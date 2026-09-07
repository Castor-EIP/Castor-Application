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
}
