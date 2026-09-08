using Castor.IA.Proto;
using CastorApplication.Models.Studio;
using CastorApplication.Services.Config;
using Grpc.Core;
using Grpc.Net.Client;

namespace CastorApplication.Services.Ai;

internal sealed class GrpcAiAnalysisClient : IAiAnalysisClient, IDisposable
{
    private readonly IConfigService _configService;
    private readonly IAiSceneStreamRuntime _sceneStreamRuntime;
    private readonly SemaphoreSlim _sync = new(1, 1);

    private GrpcChannel? _channel;
    private IaAnalysisService.IaAnalysisServiceClient? _client;
    private AsyncDuplexStreamingCall<ClientMessage, ServerEvent>? _stream;
    private CancellationTokenSource? _streamCancellation;
    private Task? _readTask;
    private Task? _keepAliveTask;
    private bool _isStopping;
    private bool _disposed;

    public bool IsAvailable => !_disposed && _sceneStreamRuntime.IsAvailable &&
        Uri.TryCreate(_configService.Config.AiServer.Endpoint, UriKind.Absolute, out _);
    public string UnavailableMessage => !_sceneStreamRuntime.IsAvailable
        ? _sceneStreamRuntime.UnavailableMessage
        : "Le serveur IA n'est pas configuré.";
    public bool HasActiveSession => !string.IsNullOrWhiteSpace(SessionId);
    public string? SessionId { get; private set; }
    public IReadOnlySet<Guid> ActiveSceneIds { get; private set; } = new HashSet<Guid>();

    public event EventHandler<AiSceneSwitchEvent>? SceneSwitchSuggested;
    public event EventHandler<AiSessionStatusEvent>? SessionStatusChanged;
    public event EventHandler<AiServerErrorEvent>? ServerErrorReceived;

    public GrpcAiAnalysisClient(IConfigService configService, IAiSceneStreamRuntime sceneStreamRuntime)
    {
        _configService = configService;
        _sceneStreamRuntime = sceneStreamRuntime;
        _sceneStreamRuntime.StateChanged += OnSceneStreamStateChanged;
    }

    public async Task StartSessionAsync(
        string moduleName,
        string mode,
        IReadOnlyList<SceneDefinition> scenes,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        if (mode is not ("agent" or "auto"))
            throw new ArgumentException("Le mode IA doit être 'agent' ou 'auto'.", nameof(mode));
        var selected = VideoScenes(scenes);
        if (selected.Count == 0)
            throw new InvalidOperationException("Sélectionnez au moins une scène avec une source vidéo.");

        await _sync.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (HasActiveSession) return;
            if (!IsAvailable) throw new InvalidOperationException(UnavailableMessage);

            // The outputs must exist before their pull URLs are advertised to the server.
            await _sceneStreamRuntime.StartStreamsAsync(selected, cancellationToken);
            try
            {
                EnsureClient();
                var response = await _client!.StartSessionAsync(new StartSessionRequest
                {
                    ModuleName = moduleName,
                    ModuleConfig = { ["mode"] = mode, ["module"] = moduleName }
                }, cancellationToken: cancellationToken);

                if (!response.Success || string.IsNullOrWhiteSpace(response.SessionId))
                    throw new InvalidOperationException(
                        $"{(string.IsNullOrWhiteSpace(response.Message) ? "Le serveur IA a refusé la session." : response.Message)} {response.ErrorCode}".Trim());

                SessionId = response.SessionId;
                // The start token only governs setup. Once the session is active,
                // its lifetime is controlled by StopSessionAsync/DisposeAsync.
                _streamCancellation = new CancellationTokenSource();
                _stream = _client.AnalysisStream(cancellationToken: _streamCancellation.Token);
                _readTask = ReadServerEventsAsync(_stream, _streamCancellation.Token);
                _keepAliveTask = KeepAliveAsync(_stream, _streamCancellation.Token);
                ActiveSceneIds = selected.Select(scene => scene.Id).ToHashSet();
                await WriteSourcesAsync(selected, cancellationToken);
            }
            catch
            {
                await ClearSessionCoreAsync();
                await _sceneStreamRuntime.StopAllAsync(CancellationToken.None);
                throw;
            }
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task UpdateSourcesAsync(
        IReadOnlyList<SceneDefinition> scenes,
        CancellationToken cancellationToken)
    {
        var desired = VideoScenes(scenes);
        await _sync.WaitAsync(cancellationToken);
        try
        {
            if (!HasActiveSession || _stream == null)
                throw new InvalidOperationException("Aucune session IA active.");
            if (desired.Count == 0)
                throw new InvalidOperationException("L'analyse IA nécessite au moins une scène vidéo.");

            var current = _sceneStreamRuntime.ActiveStreams.Keys.ToHashSet();
            var wanted = desired.Select(scene => scene.Id).ToHashSet();
            var added = desired.Where(scene => !current.Contains(scene.Id)).ToList();
            var removed = current.Except(wanted).ToArray();
            var started = Array.Empty<AiSceneStream>();

            try
            {
                if (added.Count > 0)
                    started = (await _sceneStreamRuntime.StartStreamsAsync(added, cancellationToken)).ToArray();

                await WriteSourcesAsync(desired, cancellationToken);
                ActiveSceneIds = wanted;
            }
            catch
            {
                if (started.Length > 0)
                    await _sceneStreamRuntime.StopStreamsAsync(started.Select(stream => stream.SceneId).ToArray(), CancellationToken.None);
                throw;
            }

            // Only remove old outputs after the server accepted the complete new list.
            if (removed.Length > 0)
                await _sceneStreamRuntime.StopStreamsAsync(removed, cancellationToken);
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task StopSessionAsync(string reason, CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            if (!HasActiveSession)
            {
                await _sceneStreamRuntime.StopAllAsync(cancellationToken);
                return;
            }

            _isStopping = true;
            var sessionId = SessionId!;
            try
            {
                if (_stream != null)
                {
                    try
                    {
                        await _stream.RequestStream.WriteAsync(new ClientMessage
                        {
                            SessionId = sessionId,
                            Stop = new StopSignal { Reason = reason }
                        }, cancellationToken);
                        await _stream.RequestStream.CompleteAsync();
                    }
                    catch { }
                }

                try
                {
                    if (_client != null)
                        await _client.EndSessionAsync(new EndSessionRequest { SessionId = sessionId }, cancellationToken: cancellationToken);
                }
                catch { }
            }
            finally
            {
                await ClearSessionCoreAsync();
                await _sceneStreamRuntime.StopAllAsync(CancellationToken.None);
            }
        }
        finally
        {
            _isStopping = false;
            _sync.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        try { await StopSessionAsync("desktop_dispose", CancellationToken.None); }
        catch { await _sceneStreamRuntime.StopAllAsync(CancellationToken.None); }
        _sceneStreamRuntime.StateChanged -= OnSceneStreamStateChanged;
        _channel?.Dispose();
        _sync.Dispose();
        _disposed = true;
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private async Task WriteSourcesAsync(IReadOnlyList<SceneDefinition> scenes, CancellationToken cancellationToken)
    {
        var sourceList = new SourceList();
        foreach (var scene in VideoScenes(scenes))
        {
            if (!_sceneStreamRuntime.ActiveStreams.TryGetValue(scene.Id, out var stream))
                throw new InvalidOperationException($"Le flux RTMP de la scène '{scene.Name}' n'est pas actif.");

            sourceList.Sources.Add(new Castor.IA.Proto.Source
            {
                SceneId = scene.Id.ToString("N"),
                Label = scene.Name,
                Url = stream.PullUrl,
                Metadata = { ["scene_id"] = scene.Id.ToString("N"), ["name"] = scene.Name }
            });
        }

        if (sourceList.Sources.Count == 0)
            throw new InvalidOperationException("Aucune scène vidéo sélectionnée.");
        await _stream!.RequestStream.WriteAsync(new ClientMessage
        {
            SessionId = SessionId,
            Sources = sourceList
        }, cancellationToken);
    }

    private async Task ReadServerEventsAsync(
        AsyncDuplexStreamingCall<ClientMessage, ServerEvent> stream,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var serverEvent in stream.ResponseStream.ReadAllAsync(cancellationToken))
            {
                if (!string.IsNullOrWhiteSpace(serverEvent.SessionId) && serverEvent.SessionId != SessionId) continue;
                switch (serverEvent.PayloadCase)
                {
                    case ServerEvent.PayloadOneofCase.SwitchSuggestion:
                        SceneSwitchSuggested?.Invoke(this, new AiSceneSwitchEvent(
                            serverEvent.SwitchSuggestion.SceneId, serverEvent.SwitchSuggestion.Confidence));
                        break;
                    case ServerEvent.PayloadOneofCase.Status:
                        SessionStatusChanged?.Invoke(this, new AiSessionStatusEvent(
                            serverEvent.Status.State, serverEvent.Status.Message));
                        break;
                    case ServerEvent.PayloadOneofCase.Error:
                        if (_isStopping && IsSessionNotFound(serverEvent.Error.ErrorCode)) break;
                        ServerErrorReceived?.Invoke(this, new AiServerErrorEvent(
                            serverEvent.Error.ErrorCode, serverEvent.Error.ErrorMessage, serverEvent.Error.IsFatal));
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { }
        catch (Exception ex)
        {
            if (!_isStopping)
                ServerErrorReceived?.Invoke(this, new AiServerErrorEvent("CLIENT_STREAM_ERROR", ex.Message, true));
        }
    }

    private async Task KeepAliveAsync(
        AsyncDuplexStreamingCall<ClientMessage, ServerEvent> stream,
        CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (!HasActiveSession) return;
                await stream.RequestStream.WriteAsync(new ClientMessage
                {
                    SessionId = SessionId,
                    KeepAlive = new KeepAlive { TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
                }, cancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!_isStopping)
                ServerErrorReceived?.Invoke(this, new AiServerErrorEvent("CLIENT_KEEPALIVE_ERROR", ex.Message, true));
        }
    }

    private async Task ClearSessionCoreAsync()
    {
        SessionId = null;
        ActiveSceneIds = new HashSet<Guid>();
        _streamCancellation?.Cancel();
        var tasks = new[] { _readTask, _keepAliveTask }.Where(task => task != null).Cast<Task>().ToArray();
        if (tasks.Length > 0)
        {
            try { await Task.WhenAll(tasks); } catch { }
        }
        _stream?.Dispose();
        _streamCancellation?.Dispose();
        _stream = null;
        _streamCancellation = null;
        _readTask = null;
        _keepAliveTask = null;
    }

    private void EnsureClient()
    {
        if (_client != null) return;
        _channel = GrpcChannel.ForAddress(_configService.Config.AiServer.Endpoint);
        _client = new IaAnalysisService.IaAnalysisServiceClient(_channel);
    }

    private void OnSceneStreamStateChanged(object? sender, AiSceneStreamStateChangedEventArgs e)
    {
        if (e.State == AiSceneStreamState.Error)
            ServerErrorReceived?.Invoke(this, new AiServerErrorEvent("AI_STREAM_ERROR", e.Message, true));
    }

    private static List<SceneDefinition> VideoScenes(IReadOnlyList<SceneDefinition> scenes) => scenes
        .Where(scene => scene.Id != Guid.Empty && scene.Sources.Any(source =>
            source.Kind is SourceKind.Video or SourceKind.Media))
        .GroupBy(scene => scene.Id)
        .Select(group => group.First())
        .ToList();

    private static bool IsSessionNotFound(string? errorCode) =>
        string.Equals(errorCode, "SESSION_NOT_FOUND", StringComparison.OrdinalIgnoreCase);
}
