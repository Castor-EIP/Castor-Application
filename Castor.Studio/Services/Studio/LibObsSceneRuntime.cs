using CastorApplication.Models.Settings;
using CastorApplication.Models.Studio;
using CastorApplication.Services.Settings;
using LibObs;

namespace CastorApplication.Services.Studio;

internal sealed class LibObsSceneRuntime : ISceneRuntime, ISourceRuntime, IRecordingRuntime, IScenePreviewRuntime, IDisposable
{
    private sealed record NativeSource(
        ObsSource Source,
        ObsSceneItem Item,
        bool IsMedia,
        bool ProvidesVideo);

    private readonly object _gate = new();
    private readonly Dictionary<Guid, ObsScene> _scenes = [];
    private readonly Dictionary<Guid, Dictionary<Guid, NativeSource>> _sources = [];
    private readonly SettingsService? _settingsService;
    private readonly LibObsPreviewRuntime _previewRuntime = new();
    private ObsOutput? _recordingOutput;
    private ObsEncoder? _recordingVideoEncoder;
    private ObsEncoder? _recordingAudioEncoder;
    private Guid? _recordingSceneId;
    private TaskCompletionSource<ObsOutputStateChangedEventArgs>? _recordingStarted;
    private TaskCompletionSource<ObsOutputStateChangedEventArgs>? _recordingStopped;
    private bool _recordingStopRequested;
    private bool _initialized;
    private bool _disposed;
    private string _unavailableMessage = "";
    private ObsVideoSettings? _videoSettings;
    private ApplicationSettings? _pendingVideoSettings;

    public bool IsAvailable => _initialized && !_disposed;
    public string UnavailableMessage => _unavailableMessage;

    public event EventHandler<RecordingStateChangedEventArgs>? StateChanged;
    public event EventHandler? PreviewResetRequested;

    public LibObsSceneRuntime(SettingsService? settingsService = null)
    {
        _settingsService = settingsService;
        try
        {
            Obs.Startup();
            _videoSettings = ObsMediaConfiguration.CreatePreviewVideoSettings(
                settingsService?.Load() ?? new ApplicationSettings());
            Obs.ResetVideo(_videoSettings);
            Obs.ResetAudio(new ObsAudioSettings());
            Obs.LoadModules().EnsureSuccess();
            _initialized = true;
            if (_settingsService != null)
                _settingsService.SettingsSaved += OnSettingsSaved;
        }
        catch (Exception exception)
        {
            _unavailableMessage = $"LibObs n'a pas pu être initialisé : {exception.Message}";
            TryShutdownAfterFailedStartup();
        }
    }

    public SceneRuntimeResult CreateScene(Guid sceneId, string requestedName)
    {
        if (!TryValidateRequest(requestedName, out var failure)) return failure;

        lock (_gate)
        {
            if (_scenes.ContainsKey(sceneId))
                return SceneRuntimeResult.Failure("Cette scène existe déjà dans LibObs.");

            ObsScene? scene = null;
            try
            {
                scene = ObsScene.Create(requestedName.Trim());
                var effectiveName = scene.Name;
                _scenes.Add(sceneId, scene);
                _sources.Add(sceneId, []);
                scene = null;
                return SceneRuntimeResult.Success(effectiveName);
            }
            catch (Exception exception)
            {
                scene?.Dispose();
                return SceneRuntimeResult.Failure($"Création impossible dans LibObs : {exception.Message}");
            }
        }
    }

    public SceneRuntimeResult RenameScene(Guid sceneId, string requestedName)
    {
        if (!TryValidateRequest(requestedName, out var failure)) return failure;

        lock (_gate)
        {
            if (!_scenes.TryGetValue(sceneId, out var scene))
                return SceneRuntimeResult.Failure("Cette scène n'existe pas dans LibObs.");

            try
            {
                using var source = scene.Source;
                source.Name = requestedName.Trim();
                return SceneRuntimeResult.Success(scene.Name);
            }
            catch (Exception exception)
            {
                return SceneRuntimeResult.Failure($"Renommage impossible dans LibObs : {exception.Message}");
            }
        }
    }

    public SceneRuntimeResult RemoveScene(Guid sceneId)
    {
        if (!IsAvailable) return SceneRuntimeResult.Unavailable(UnavailableMessageForOperation());

        lock (_gate)
        {
            if (_recordingSceneId == sceneId)
                return SceneRuntimeResult.Failure("Cette scène est utilisée par l'enregistrement en cours.");
            if (!_scenes.TryGetValue(sceneId, out var scene))
                return SceneRuntimeResult.Failure("Cette scène n'existe pas dans LibObs.");

            try
            {
                _previewRuntime.Stop(sceneId);

                if (_sources.TryGetValue(sceneId, out var sources))
                {
                    foreach (var nativeSource in sources.Values)
                        RemoveNativeSource(nativeSource);
                    sources.Clear();
                }

                scene.Remove();
                scene.Dispose();
                _sources.Remove(sceneId);
                _scenes.Remove(sceneId);
                return SceneRuntimeResult.Success();
            }
            catch (Exception exception)
            {
                return SceneRuntimeResult.Failure($"Suppression impossible dans LibObs : {exception.Message}");
            }
        }
    }

    public Task<SourceCatalog> EnumerateSourcesAsync(CancellationToken cancellationToken) =>
        Task.Run(() => EnumerateSources(cancellationToken), cancellationToken);

    public SourceRuntimeResult AddSource(Guid sceneId, SourceAddRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsAvailable) return SourceRuntimeResult.Unavailable(UnavailableMessageForOperation());
        if (request.SourceId == Guid.Empty)
            return SourceRuntimeResult.Failure("L'identifiant de la source est obligatoire.");
        if (string.IsNullOrWhiteSpace(request.RequestedName))
            return SourceRuntimeResult.Failure("Le nom de la source est obligatoire.");

        lock (_gate)
        {
            if (!IsAvailable) return SourceRuntimeResult.Unavailable(UnavailableMessageForOperation());
            if (!_scenes.TryGetValue(sceneId, out var scene) || !_sources.TryGetValue(sceneId, out var sources))
                return SourceRuntimeResult.Failure("Cette scène n'existe pas dans LibObs.");
            if (sources.ContainsKey(request.SourceId))
                return SourceRuntimeResult.Failure("Cette source existe déjà dans LibObs.");

            ObsSource? source = null;
            ObsSceneItem? item = null;
            try
            {
                //source = CreateNativeSource(request);
                source = ObsSourceFactory.Create(request);
                item = scene.Add(source);
                var effectiveName = source.Name;
                sources.Add(request.SourceId, new NativeSource(
                    source,
                    item,
                    request is SourceAddRequest.Media,
                    request is not SourceAddRequest.Audio));
                source = null;
                item = null;
                return SourceRuntimeResult.Success(effectiveName);
            }
            catch (Exception exception)
            {
                TryRollbackSource(source, item);
                return SourceRuntimeResult.Failure($"Ajout impossible dans LibObs : {exception.Message}");
            }
        }
    }

    public SourceRuntimeResult RemoveSource(Guid sceneId, Guid sourceId)
    {
        if (!IsAvailable) return SourceRuntimeResult.Unavailable(UnavailableMessageForOperation());

        lock (_gate)
        {
            if (!IsAvailable) return SourceRuntimeResult.Unavailable(UnavailableMessageForOperation());
            if (!_sources.TryGetValue(sceneId, out var sources) || !sources.TryGetValue(sourceId, out var source))
                return SourceRuntimeResult.Failure("Cette source n'existe pas dans LibObs.");

            try
            {
                RemoveNativeSource(source);
                sources.Remove(sourceId);
                return SourceRuntimeResult.Success();
            }
            catch (Exception exception)
            {
                return SourceRuntimeResult.Failure($"Suppression impossible dans LibObs : {exception.Message}");
            }
        }
    }

    public SourceRuntimeResult SetMediaLoop(Guid sceneId, Guid sourceId, bool loop)
    {
        if (!IsAvailable) return SourceRuntimeResult.Unavailable(UnavailableMessageForOperation());

        lock (_gate)
        {
            if (!IsAvailable) return SourceRuntimeResult.Unavailable(UnavailableMessageForOperation());
            if (!_sources.TryGetValue(sceneId, out var sources) || !sources.TryGetValue(sourceId, out var source))
                return SourceRuntimeResult.Failure("Cette source n'existe pas dans LibObs.");
            if (!source.IsMedia)
                return SourceRuntimeResult.Failure("Seules les sources média peuvent être lues en boucle.");

            try
            {
                using var settings = new ObsData();
                settings.SetBool(ObsKnownSettings.Media.Looping, loop);
                source.Source.Update(settings);
                return SourceRuntimeResult.Success(source.Source.Name);
            }
            catch (Exception exception)
            {
                return SourceRuntimeResult.Failure($"Mise à jour impossible dans LibObs : {exception.Message}");
            }
        }
    }

    private void OnSettingsSaved(object? sender, EventArgs e)
    {
        var settings = _settingsService?.Load();
        if (settings == null) return;

        lock (_gate)
        {
            if (!IsAvailable) return;
            if (_recordingOutput != null)
            {
                _pendingVideoSettings = settings;
                return;
            }

            ApplyVideoSettingsCore(settings);
        }
    }

    private void ApplyVideoSettingsCore(ApplicationSettings settings)
    {
        var next = ObsMediaConfiguration.CreatePreviewVideoSettings(settings);

        if (ObsMediaConfiguration.AreSameVideoSettings(_videoSettings, next))
        {
            _pendingVideoSettings = null;
            return;
        }

        _pendingVideoSettings = null;
        _previewRuntime.Dispose();
        try
        {
            Obs.ResetVideo(next);
            _videoSettings = next;
        }
        catch
        {
            // The next preview start reports the LibObs error while keeping the
            // runtime alive for existing scenes and sources.
        }

        PreviewResetRequested?.Invoke(this, EventArgs.Empty);
    }

    public Task<StudioRuntimeResult> StartPreviewAsync(
        SceneDefinition scene,
        IntPtr windowHandle,
        uint width,
        uint height,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scene);
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
            return Task.FromResult(StudioRuntimeResult.Unavailable("La preview LibObs nécessite Windows."));
        if (windowHandle == IntPtr.Zero)
            return Task.FromResult(StudioRuntimeResult.Failure("Le handle de la surface de preview est invalide."));

        lock (_gate)
        {
            if (!IsAvailable) return Task.FromResult(StudioRuntimeResult.Unavailable(UnavailableMessageForOperation()));
            if (!_scenes.TryGetValue(scene.Id, out var nativeScene))
                return Task.FromResult(StudioRuntimeResult.Failure("Cette scène n'existe pas dans LibObs."));
            return Task.FromResult(_previewRuntime.Start(
                scene.Id,
                nativeScene,
                windowHandle,
                width,
                height,
                _videoSettings?.BaseWidth ?? 1920,
                _videoSettings?.BaseHeight ?? 1080));
        }
    }

    public void ResizePreview(uint width, uint height)
    {
        if (width == 0 || height == 0) return;

        lock (_gate)
        {
            if (!IsAvailable) return;
            _previewRuntime.Resize(width, height);
        }
    }

    public Task<StudioRuntimeResult> StopPreviewAsync(Guid sceneId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _previewRuntime.Stop(sceneId);
        }

        return Task.FromResult(StudioRuntimeResult.Success());
    }

    public async Task<StudioRuntimeResult> StartRecordingAsync(
        RecordingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Task<ObsOutputStateChangedEventArgs> startedTask;

        lock (_gate)
        {
            if (!IsAvailable) return StudioRuntimeResult.Unavailable(UnavailableMessageForOperation());
            if (_recordingOutput != null)
                return StudioRuntimeResult.Failure("Un enregistrement est déjà en cours.");
            if (!_scenes.TryGetValue(request.SceneId, out var scene))
                return StudioRuntimeResult.Failure("Cette scène n'existe pas dans LibObs.");
            if (!_sources.TryGetValue(request.SceneId, out var sources) ||
                !sources.Values.Any(source => source.ProvidesVideo))
                return StudioRuntimeResult.Failure(
                    "La scène doit contenir au moins une source vidéo ou média.");

            var validationError = ValidateRecordingRequest(request);
            if (validationError.Length > 0) return StudioRuntimeResult.Failure(validationError);

            try
            {
                EnsureVideoSettingsForRecording(request);
                ObsMediaConfiguration.ConfigureRecordingAudio(request);
                using (var sceneSource = scene.Source)
                    Obs.SetOutputSource(0, sceneSource);

                var resources = ObsRecordingOutputFactory.Create(request);

                _recordingOutput = resources.Output;
                _recordingVideoEncoder = resources.VideoEncoder;
                _recordingAudioEncoder = resources.AudioEncoder;
                _recordingSceneId = request.SceneId;
                _recordingStarted = NewOutputSignal();
                _recordingStopped = NewOutputSignal();
                _recordingOutput.StateChanged += OnRecordingOutputStateChanged;
                startedTask = _recordingStarted.Task;
                _recordingOutput.Start();
            }
            catch (Exception exception)
            {
                ReleaseRecordingResourcesCore();
                return StudioRuntimeResult.Failure($"Démarrage de l'enregistrement impossible : {exception.Message}");
            }
        }

        try
        {
            var state = await startedTask.WaitAsync(cancellationToken);
            return state.State == ObsOutputState.Started
                ? StudioRuntimeResult.Success()
                : StudioRuntimeResult.Failure(RecordingStopMessage(state));
        }
        catch (OperationCanceledException)
        {
            lock (_gate)
            {
                try
                {
                    _recordingOutput?.ForceStop();
                }
                catch
                {
                }
                ReleaseRecordingResourcesCore();
            }
            throw;
        }
    }

    public async Task<StudioRuntimeResult> StopRecordingAsync(CancellationToken cancellationToken)
    {
        Task<ObsOutputStateChangedEventArgs> stoppedTask;
        ObsOutput output;
        lock (_gate)
        {
            if (_recordingOutput == null || _recordingStopped == null)
                return StudioRuntimeResult.Success();

            output = _recordingOutput;
            stoppedTask = _recordingStopped.Task;
            try
            {
                _recordingStopRequested = true;
                output.Stop();
            }
            catch (Exception exception)
            {
                _recordingStopRequested = false;
                return StudioRuntimeResult.Failure($"Arrêt de l'enregistrement impossible : {exception.Message}");
            }
        }

        ObsOutputStateChangedEventArgs state;
        try
        {
            state = await stoppedTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            lock (_gate)
            {
                if (ReferenceEquals(output, _recordingOutput)) ReleaseRecordingResourcesCore();
            }
            throw;
        }
        lock (_gate)
        {
            if (ReferenceEquals(output, _recordingOutput)) ReleaseRecordingResourcesCore();
        }
        return state.StopCode is null or ObsOutputStopCode.Success
            ? StudioRuntimeResult.Success()
            : StudioRuntimeResult.Failure(RecordingStopMessage(state));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            if (_settingsService != null)
                _settingsService.SettingsSaved -= OnSettingsSaved;

            if (_recordingOutput != null)
            {
                try
                {
                    _recordingOutput.ForceStop();
                }
                catch
                {
                }
            }
            ReleaseRecordingResourcesCore();
            _previewRuntime.Dispose();

            foreach (var sceneSources in _sources.Values)
            {
                foreach (var nativeSource in sceneSources.Values)
                    DisposeNativeSource(nativeSource);
            }
            _sources.Clear();

            foreach (var scene in _scenes.Values)
                scene.Dispose();
            _scenes.Clear();

            if (_initialized)
            {
                Obs.Shutdown();
                _initialized = false;
            }
        }
    }

    private static TaskCompletionSource<ObsOutputStateChangedEventArgs> NewOutputSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static string ValidateRecordingRequest(RecordingRequest request)
    {
        if (request.SceneId == Guid.Empty) return "L'identifiant de la scène est obligatoire.";
        if (string.IsNullOrWhiteSpace(request.OutputPath) || !Path.IsPathFullyQualified(request.OutputPath))
            return "Le chemin du fichier de sortie doit être absolu.";
        if (request.Fps <= 0 || request.VideoBitrateKbps <= 0 || request.AudioBitrateKbps <= 0)
            return "Les débits et le nombre d'images par seconde doivent être supérieurs à zéro.";
        if (request.AudioSampleRate <= 0 || request.AudioChannels is < 1 or > 2)
            return "La configuration audio doit être mono ou stéréo avec une fréquence valide.";
        if (request.BaseWidth <= 0 || request.BaseHeight <= 0 || request.OutputWidth <= 0 || request.OutputHeight <= 0)
            return "Les résolutions vidéo doivent être supérieures à zéro.";
        return "";
    }

    private void EnsureVideoSettingsForRecording(RecordingRequest request)
    {
        var desired =
            ObsMediaConfiguration.CreateRecordingVideoSettings(request);

        if (ObsMediaConfiguration.AreSameVideoSettings(_videoSettings, desired))
            return;

        _previewRuntime.Dispose();
        Obs.ResetVideo(desired);
        _videoSettings = desired;
        PreviewResetRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnRecordingOutputStateChanged(object? sender, ObsOutputStateChangedEventArgs args)
    {
        TaskCompletionSource<ObsOutputStateChangedEventArgs>? started = null;
        TaskCompletionSource<ObsOutputStateChangedEventArgs>? stopped = null;
        RecordingStateChangedEventArgs? notification = null;
        ObsOutput? unexpectedlyStoppedOutput = null;

        lock (_gate)
        {
            if (!ReferenceEquals(sender, _recordingOutput)) return;

            if (args.State == ObsOutputState.Started)
            {
                started = _recordingStarted;
                notification = new RecordingStateChangedEventArgs(true);
            }
            else if (args.State == ObsOutputState.Stopped)
            {
                started = _recordingStarted;
                stopped = _recordingStopped;
                notification = new RecordingStateChangedEventArgs(false, RecordingStopMessage(args));
                _recordingSceneId = null;
                if (!_recordingStopRequested) unexpectedlyStoppedOutput = _recordingOutput;
            }
        }

        started?.TrySetResult(args);
        stopped?.TrySetResult(args);
        if (notification != null) StateChanged?.Invoke(this, notification);
        if (unexpectedlyStoppedOutput != null)
            _ = ReleaseUnexpectedlyStoppedOutputAsync(unexpectedlyStoppedOutput);
    }

    private async Task ReleaseUnexpectedlyStoppedOutputAsync(ObsOutput output)
    {
        // ffmpeg_output emits its stop signal before its plugin stop callback has
        // necessarily finished writing the trailer.
        await Task.Delay(100);
        lock (_gate)
        {
            if (ReferenceEquals(output, _recordingOutput)) ReleaseRecordingResourcesCore();
        }
    }

    private void ReleaseRecordingResourcesCore()
    {
        var output = _recordingOutput;
        var videoEncoder = _recordingVideoEncoder;
        var audioEncoder = _recordingAudioEncoder;

        _recordingOutput = null;
        _recordingVideoEncoder = null;
        _recordingAudioEncoder = null;
        _recordingSceneId = null;
        _recordingStarted = null;
        _recordingStopped = null;
        _recordingStopRequested = false;

        if (output != null) output.StateChanged -= OnRecordingOutputStateChanged;
        try
        {
            if (_initialized) Obs.SetOutputSource(0, null);
        }
        catch
        {
        }
        try
        {
            output?.Dispose();
        }
        catch
        {
        }
        try
        {
            audioEncoder?.Dispose();
        }
        catch
        {
        }
        try
        {
            videoEncoder?.Dispose();
        }
        catch
        {
        }

        if (!_disposed && _pendingVideoSettings != null)
            ApplyVideoSettingsCore(_pendingVideoSettings);
    }

    private static string RecordingStopMessage(ObsOutputStateChangedEventArgs state)
    {
        if (!string.IsNullOrWhiteSpace(state.Error)) return state.Error;
        return state.StopCode switch
        {
            null or ObsOutputStopCode.Success => "",
            ObsOutputStopCode.BadPath => "Le chemin du fichier de sortie est invalide.",
            ObsOutputStopCode.NoSpace => "Espace disque insuffisant pour poursuivre l'enregistrement.",
            ObsOutputStopCode.EncodeError => "L'encodeur vidéo ou audio a rencontré une erreur.",
            ObsOutputStopCode.Unsupported => "Le format d'enregistrement n'est pas pris en charge.",
            _ => $"L'enregistrement s'est arrêté avec le code {state.StopCode}."
        };
    }

    private SourceCatalog EnumerateSources(CancellationToken ct)
    {
        if (!IsAvailable)
            return new([], [], UnavailableMessageForOperation());

        lock (_gate)
        {
            if (!IsAvailable)
                return new([], [], UnavailableMessageForOperation());

            return ObsSourceCatalog.Enumerate(ct);
        }
    }

    private static void RemoveNativeSource(NativeSource nativeSource)
    {
        nativeSource.Item.Remove();
        nativeSource.Item.Dispose();
        nativeSource.Source.Remove();
        nativeSource.Source.Dispose();
    }

    private static void DisposeNativeSource(NativeSource nativeSource)
    {
        try
        {
            nativeSource.Item.Dispose();
        }
        finally
        {
            nativeSource.Source.Dispose();
        }
    }

    private static void TryRollbackSource(ObsSource? source, ObsSceneItem? item)
    {
        try
        {
            item?.Remove();
        }
        catch
        {
        }
        finally
        {
            item?.Dispose();
        }

        try
        {
            source?.Remove();
        }
        catch
        {
        }
        finally
        {
            source?.Dispose();
        }
    }

    private bool TryValidateRequest(string requestedName, out SceneRuntimeResult failure)
    {
        if (!IsAvailable)
        {
            failure = SceneRuntimeResult.Unavailable(UnavailableMessageForOperation());
            return false;
        }

        if (string.IsNullOrWhiteSpace(requestedName))
        {
            failure = SceneRuntimeResult.Failure("Le nom de la scène est obligatoire.");
            return false;
        }

        failure = SceneRuntimeResult.Success();
        return true;
    }

    private string UnavailableMessageForOperation() =>
        string.IsNullOrWhiteSpace(_unavailableMessage)
            ? "LibObs n'est pas disponible."
            : _unavailableMessage;

    private static void TryShutdownAfterFailedStartup()
    {
        try
        {
            if (Obs.IsInitialized) Obs.Shutdown();
        }
        catch
        {
            // Preserve the initialization failure; there are no managed OBS handles yet.
        }
    }
}
