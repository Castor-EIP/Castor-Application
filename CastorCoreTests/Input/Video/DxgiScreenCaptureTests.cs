using CastorCore.Input.Screen;
using CastorCore.Source;
using FFMpegCore.Pipes;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace CastorCoreTests.Input.Video
{
    [CollectionDefinition("DxgiCapture", DisableParallelization = true)]
    public class DxgiCaptureCollection
    {
    }

    [Collection("DxgiCapture")]
    public class DxgiScreenCaptureTests
    {
        [Fact]
        public void TestDxgiScreenCaptureInitialization()
        {
            using DxgiScreenCapture capture = new DxgiScreenCapture(0);

            Assert.True(capture.Width > 0, "Width should be positive");
            Assert.True(capture.Height > 0, "Height should be positive");
            Assert.Equal("bgra32", capture.Format);
        }

        [Fact]
        public async Task TestCaptureFrame()
        {
            using DxgiScreenCapture capture = new DxgiScreenCapture(0);
            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            IVideoFrame? frame = null;
            int attempts = 0;
            while (frame == null && attempts < 100 && !cts.Token.IsCancellationRequested)
            {
                frame = await capture.CaptureFrameAsync(cts.Token);
                attempts++;
                if (frame == null)
                {
                    await Task.Delay(10, cts.Token);
                }
            }

            Assert.NotNull(frame);
            Assert.Equal(capture.Width, frame.Width);
            Assert.Equal(capture.Height, frame.Height);
            Assert.Equal("bgra32", frame.Format);

            if (frame is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        [Fact]
        public async Task TestCaptureMultipleFrames()
        {
            await Task.Delay(100);
            
            using DxgiScreenCapture capture = new DxgiScreenCapture(0);
            using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            int frameCount = 0;
            int targetFrames = 10;

            while (frameCount < targetFrames && !cts.Token.IsCancellationRequested)
            {
                IVideoFrame? frame = await capture.CaptureFrameAsync(cts.Token);
                if (frame != null)
                {
                    frameCount++;
                    Assert.Equal(capture.Width, frame.Width);
                    Assert.Equal(capture.Height, frame.Height);

                    if (frame is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                else
                {
                    await Task.Delay(10, cts.Token);
                }
            }

            Assert.Equal(targetFrames, frameCount);
        }

        [Fact]
        public void TestInvalidMonitorId()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DxgiScreenCapture(999));
        }
    }
}
