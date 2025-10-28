namespace CastorCore.Source.Frame
{
    public enum VideoPixelFormat
    {
        BGRA32,
        RGBA32,
        NV12,
        I420
    }

    public class VideoFrame : MediaFrame
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] Data { get; set; }
        public VideoPixelFormat Format { get; set; }

        public override void Dispose()
        {
            if (!_disposed)
            {
                Data = null;
                _disposed = true;
            }
        }
    }
}
