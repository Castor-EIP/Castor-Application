using CastorApplication.Models.Config;
using CastorApplication.Models.Studio;

namespace CastorApplication.Services.Ai;

internal sealed record AiSceneStream(Guid SceneId, string Name, string PullUrl);

internal enum AiSceneStreamState { Started, Stopped, Reconnecting, Error }

internal sealed class AiSceneStreamStateChangedEventArgs(
    Guid sceneId, AiSceneStreamState state, string message = "") : EventArgs
{
    public Guid SceneId { get; } = sceneId;
    public AiSceneStreamState State { get; } = state;
    public string Message { get; } = message;
}

internal interface IAiSceneStreamRuntime : IDisposable
{
    bool IsAvailable { get; }
    string UnavailableMessage { get; }
    IReadOnlyDictionary<Guid, AiSceneStream> ActiveStreams { get; }
    event EventHandler<AiSceneStreamStateChangedEventArgs>? StateChanged;

    Task<IReadOnlyList<AiSceneStream>> StartStreamsAsync(
        IReadOnlyList<SceneDefinition> scenes, CancellationToken cancellationToken);
    Task StopStreamsAsync(IReadOnlyCollection<Guid> sceneIds, CancellationToken cancellationToken);
    Task StopAllAsync(CancellationToken cancellationToken);
}

internal static class AiSceneStreamConfiguration
{
    public const string DefaultRtmpBaseUrl = "rtmp://127.0.0.1:1935/live/";

    public static string BuildPullUrl(string? baseUrl, Guid sceneId)
    {
        if (sceneId == Guid.Empty)
            throw new ArgumentException("L'identifiant de scène est obligatoire.", nameof(sceneId));

        var prefix = string.IsNullOrWhiteSpace(baseUrl) ? DefaultRtmpBaseUrl : baseUrl.Trim();
        if (!prefix.EndsWith('/')) prefix += "/";
        return $"{prefix}{sceneId:N}";
    }

    public static string BaseUrl(AiServerConfig config) =>
        string.IsNullOrWhiteSpace(config.MediaMtxRtmpBaseUrl)
            ? DefaultRtmpBaseUrl
            : config.MediaMtxRtmpBaseUrl;
}
