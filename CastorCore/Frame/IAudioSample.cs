using FFMpegCore.Pipes;

namespace CastorCore.Frame
{
    public interface IAudioSample : IVideoFrame
    {
        byte[] Data { get; }
        int Channels { get; }
        int SampleRate { get; }
        int SampleCount { get; }
        
        TimeSpan Timestamp { get; }
    }
}