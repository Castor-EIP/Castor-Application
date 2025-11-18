using CastorCore.Source;
using CastorCoreTests.Mock;
using FFMpegCore.Pipes;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CastorCoreTests
{
    public class VideoSourceTests
    {
        [Fact]
        public async Task TestVideoSourceWithMockInput()
        {
            // Arrange
            MockVideoInput mockInput = new MockVideoInput();
            VideoSource testSource = new VideoSource(mockInput);
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            List<IVideoFrame> capturedFrames = new List<IVideoFrame>();

            // Act
            testSource.Start();
            
            while (capturedFrames.Count < 5 && !cts.Token.IsCancellationRequested)
            {
                IVideoFrame? frame = await testSource.GetFrameAsync(cts.Token);
                if (frame != null)
                {
                    capturedFrames.Add(frame);
                }
            }
            
            testSource.Stop();

            // Assert
            Assert.True(capturedFrames.Count >= 5, $"Expected at least 5 frames, got {capturedFrames.Count}");
            
            foreach (IVideoFrame frame in capturedFrames)
            {
                Assert.Equal(640, frame.Width);
                Assert.Equal(480, frame.Height);
                Assert.Equal("bgra32", frame.Format);
            }
        }

        [Fact]
        public void TestVideoSourceDispose()
        {
            // Arrange
            MockVideoInput mockInput = new MockVideoInput();
            VideoSource testSource = new VideoSource(mockInput);

            // Act
            testSource.Dispose();

            // Assert - Should not throw
            Assert.True(true);
        }

        [Fact]
        public async Task TestVideoSourceMultipleFrames()
        {
            // Arrange
            MockVideoInput mockInput = new MockVideoInput();
            VideoSource testSource = new VideoSource(mockInput);
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            List<IVideoFrame> capturedFrames = new List<IVideoFrame>();
            int targetFrames = 10;

            // Act
            testSource.Start();
            
            while (capturedFrames.Count < targetFrames && !cts.Token.IsCancellationRequested)
            {
                IVideoFrame? frame = await testSource.GetFrameAsync(cts.Token);
                if (frame != null)
                {
                    capturedFrames.Add(frame);
                }
            }
            
            testSource.Stop();

            // Assert
            Assert.True(capturedFrames.Count >= targetFrames, $"Expected at least {targetFrames} frames, got {capturedFrames.Count}");
        }

        [Fact]
        public async Task TestVideoSourceStopsOnCancellation()
        {
            // Arrange
            MockVideoInput mockInput = new MockVideoInput();
            VideoSource testSource = new VideoSource(mockInput);
            
            using CancellationTokenSource cts = new CancellationTokenSource();

            // Act
            testSource.Start();

            Task task = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    await testSource.GetFrameAsync(cts.Token);
                }
            });

            // Cancel after 100ms
            await Task.Delay(100);
            cts.Cancel();
            testSource.Stop();
            
            // Wait for the task to complete with cancellation
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation occurs
            }

            // Assert - Should complete without throwing unhandled exception
            Assert.True(true);
        }
    }
}
