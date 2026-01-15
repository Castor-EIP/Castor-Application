using CastorCore.Encoder;
using CastorCore.Source.Audio;
using CastorCore.Source.Video;
using FFMpegCore.Pipes;

namespace CastorCore
{
    public sealed class Pipeline
    {
        private IVideoSource _videoSource;

        private IPipeSource _videoPipe;

        private Mp4Encoder _encoder;

        public Pipeline(IVideoSource videoSource)
        {
            _videoSource = videoSource ?? throw new ArgumentNullException(nameof(videoSource));
            _videoPipe = _videoSource.ToPipeSource();

            string outputPath = $"output_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";

            _encoder = new Mp4Encoder(_videoPipe, outputPath);
        }

        public async Task Start(CancellationToken cts = default)
        {
            Console.WriteLine("[Pipeline] Starting video source...");
            _videoSource.StartCapture();
            Console.WriteLine("[Pipeline] Starting encoder...");
            await _encoder.StartAsync(cts);

            return;
        }

        public void Stop()
        {
            Console.WriteLine("[Pipeline] Stopping video source...");
            _videoSource.StopCapture();
        }
    }
}
