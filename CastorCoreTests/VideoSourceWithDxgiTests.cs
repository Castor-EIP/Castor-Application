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

            source.Start();

            List<IVideoFrame> capturedFrames = new List<IVideoFrame>();
            
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var frame = await source.GetFrameAsync(cts.Token);
                    if (frame != null)
                    {
                        capturedFrames.Add(frame);
                        if (capturedFrames.Count >= 30)
                        {
                            break;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when timeout occurs
            }
            finally
            {
                source.Stop();
            }

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
            
            using DxgiScreenCapture input = new DxgiScreenCapture(0);
            using VideoSource source = new VideoSource(input);
            using CancellationTokenSource cts = new CancellationTokenSource();

            source.Start();

            List<IVideoFrame> capturedFrames = new List<IVideoFrame>();

            Task task = Task.Run(async () =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        IVideoFrame? frame = await source.GetFrameAsync(cts.Token);
                        if (frame != null)
                        {
                            capturedFrames.Add(frame);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            });

            await Task.Delay(500);
            cts.Cancel();
            await task;
            source.Stop();

            Assert.True(capturedFrames.Count > 0, "Should have captured at least one frame");
            
            foreach (IVideoFrame frame in capturedFrames)
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
            
            using DxgiScreenCapture input = new DxgiScreenCapture(0);
            using VideoSource source = new VideoSource(input);
            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            source.Start();

            int frameCount = 0;
            
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    IVideoFrame? frame = await source.GetFrameAsync(cts.Token);
                    if (frame != null)
                    {
                        frameCount++;
                        if (frameCount >= 5)
                        {
                            break;
                        }
                        
                        if (frame is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when timeout occurs
            }
            finally
            {
                source.Stop();
            }

            Assert.True(frameCount >= 5, $"Expected at least 5 frames, got {frameCount}");
        }
    }
}
