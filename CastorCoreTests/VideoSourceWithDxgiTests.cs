using CastorCore.Input.Screen;
using CastorCore.Source;
using FFMpegCore.Pipes;

namespace CastorCoreTests
{
    [Collection("DxgiCapture")]
    public class VideoSourceWithDxgiTests
    {
        [Fact]
        public async Task TestVideoSourceWithRealCapture()
        {
            await Task.Delay(100);
            
            using var input = new DxgiScreenCapture(0);
            using var source = new VideoSource(input);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var capturedFrames = new List<IVideoFrame>();
            source.FrameReady += (frame) =>
            {
                capturedFrames.Add(frame);
                if (capturedFrames.Count >= 30)
                {
                    cts.Cancel();
                }
            };

            await source.StartAsync(cts.Token);

            Assert.True(capturedFrames.Count >= 10, $"Expected at least 10 frames, got {capturedFrames.Count}");
            
            foreach (var frame in capturedFrames)
            {
                Assert.Equal(input.Width, frame.Width);
                Assert.Equal(input.Height, frame.Height);
                
                if (frame is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        [Fact]
        public async Task TestVideoSourceStopsCleanly()
        {
            await Task.Delay(100);
            
            using var input = new DxgiScreenCapture(0);
            using var source = new VideoSource(input);
            using var cts = new CancellationTokenSource();

            var capturedFrames = new List<IVideoFrame>();
            source.FrameReady += (frame) =>
            {
                capturedFrames.Add(frame);
            };

            var task = Task.Run(async () => await source.StartAsync(cts.Token));
            await Task.Delay(500);
            cts.Cancel();
            await task;

            Assert.True(capturedFrames.Count > 0, "Should have captured at least one frame");
            
            foreach (var frame in capturedFrames)
            {
                if (frame is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        [Fact]
        public async Task TestVideoSourceEventFiring()
        {
            await Task.Delay(100);
            
            using var input = new DxgiScreenCapture(0);
            using var source = new VideoSource(input);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            int eventCount = 0;
            source.FrameReady += (frame) =>
            {
                eventCount++;
                if (eventCount >= 5)
                {
                    cts.Cancel();
                }
                
                if (frame is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            };

            await source.StartAsync(cts.Token);

            Assert.True(eventCount >= 5, $"Expected at least 5 events, got {eventCount}");
        }
    }
}
