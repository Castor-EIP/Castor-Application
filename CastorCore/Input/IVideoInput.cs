using FFMpegCore.Pipes;

namespace CastorCore.Input
{
    public interface IVideoInput : IDisposable
    {
        int Width { get; }
        int Height { get; }
        string Format { get; } // bgra32, nv12, etc.

        Task<IVideoFrame?> CaptureFrameAsync(CancellationToken token);
    }
}