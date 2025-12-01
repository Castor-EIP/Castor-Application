using FFMpegCore.Pipes;

namespace CastorCore.Source.Video
{
    public interface IVideoSource
    {
        int Width { get; }
        int Height { get; }

        IEnumerable<IVideoFrame> GetVideoFrames();
        IPipeSource ToPipeSource();
    }
}
