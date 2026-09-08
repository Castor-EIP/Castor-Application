using CastorApplication.Models.Studio;

namespace CastorApplication.Services.Studio;

internal sealed class StreamingStateChangedEventArgs(bool isStreaming, string message = "") : EventArgs
{
    public bool IsStreaming { get; } = isStreaming;
    public string Message { get; } = message;
}

internal interface IStreamingRuntime
{
    bool IsAvailable { get; }
    string UnavailableMessage { get; }

    event EventHandler<StreamingStateChangedEventArgs>? StreamingStateChanged;

    Task<StudioRuntimeResult> StartStreamingAsync(StreamingRequest request, CancellationToken cancellationToken);
    Task<StudioRuntimeResult> StopStreamingAsync(CancellationToken cancellationToken);

    // Re-points the running output at another scene. StartStreamingAsync pins the OBS
    // program channel to the scene that was active then, so a global scene switch has to
    // update the running live output as well.
    StudioRuntimeResult SwitchStreamingScene(Guid sceneId);
}
