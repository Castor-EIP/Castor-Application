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
            var capturedFrames = new List<IVideoFrame>();

            testSource.FrameReady += (frame) =>
            {
                capturedFrames.Add(frame);
                if (capturedFrames.Count >= 5)
                {
                    cts.Cancel();
                }
            };

            // Act
            await testSource.StartAsync(cts.Token);

            // Assert
            Assert.True(capturedFrames.Count >= 5, $"Expected at least 5 frames, got {capturedFrames.Count}");
            
            foreach (var frame in capturedFrames)
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
            var capturedFrames = new List<IVideoFrame>();
            int targetFrames = 10;

            testSource.FrameReady += (frame) =>
            {
                capturedFrames.Add(frame);
                if (capturedFrames.Count >= targetFrames)
                {
                    cts.Cancel();
                }
            };

            // Act
            await testSource.StartAsync(cts.Token);

            // Assert
            Assert.True(capturedFrames.Count >= targetFrames, $"Expected at least {targetFrames} frames, got {capturedFrames.Count}");
        }

        [Fact]
        public async Task TestVideoSourceStopsOnCancellation()
        {
            // Arrange
            MockVideoInput mockInput = new MockVideoInput();
            VideoSource testSource = new VideoSource(mockInput);
            
            using var cts = new CancellationTokenSource();
            var task = Task.Run(async () => await testSource.StartAsync(cts.Token));

            // Act - Cancel after 100ms
            await Task.Delay(100);
            cts.Cancel();
            
            // Wait for the task to complete
            await task;

            // Assert - Should complete without throwing
            Assert.True(true);
        }
    }
}
