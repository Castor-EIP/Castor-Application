using CastorCore.Source;
using CastorCore.Source.Frame;
using CastorCoreTests.Mock;

namespace CastorCoreTests
{
    public class VideoSourceTests
    {
        [Fact]
        public async void TestVideoSourceInitialization()
        {
            MockVideoInput mockInput = new MockVideoInput();
            VideoSource testSource = new VideoSource("Test", mockInput);

            Assert.Equal(SourceState.Idle, testSource.State);

            await testSource.InitializeAsync();
            Assert.Equal(SourceState.Stopped, testSource.State);

        }

        [Fact]
        public async void TestVideoSourceReadFrame()
        {
            MockVideoInput mockInput = new MockVideoInput();
            VideoSource testSource = new VideoSource("Test", mockInput);

            Assert.Equal(SourceState.Idle, testSource.State);

            await testSource.InitializeAsync();
            Assert.Equal(SourceState.Stopped, testSource.State);

            testSource.Start();
            Assert.Equal(SourceState.Running, testSource.State);

            VideoFrame? frame = await testSource.GetNextFrameAsync();

            Assert.NotNull(frame);
            Assert.Equal(640, frame.Width);
            Assert.Equal(480, frame.Height);
            Assert.Equal(640 * 480 * 4, frame.Data.Length);
        }

        [Fact]
        public async void TestVideoSourcePauseRestart()
        {
            MockVideoInput mockInput = new MockVideoInput();
            VideoSource testSource = new VideoSource("Test", mockInput);

            Assert.Equal(SourceState.Idle, testSource.State);

            await testSource.InitializeAsync();
            Assert.Equal(SourceState.Stopped, testSource.State);

            testSource.Start();
            Assert.Equal(SourceState.Running, testSource.State);

            testSource.Pause();
            Assert.Equal(SourceState.Paused, testSource.State);

            testSource.Start();
            Assert.Equal(SourceState.Running, testSource.State);
        }

        [Fact]
        public async void TestVideoSourceStop()
        {
            MockVideoInput mockInput = new MockVideoInput();
            VideoSource testSource = new VideoSource("Test", mockInput);

            Assert.Equal(SourceState.Idle, testSource.State);

            await testSource.InitializeAsync();
            Assert.Equal(SourceState.Stopped, testSource.State);

            testSource.Start();
            Assert.Equal(SourceState.Running, testSource.State);

            testSource.Stop();
            Assert.Equal(SourceState.Stopped, testSource.State);
        }

        [Fact]
        public async void TestVideoSourceReadFrameWhenNotRunning()
        {
            MockVideoInput mockInput = new MockVideoInput();
            VideoSource testSource = new VideoSource("Test", mockInput);

            Assert.Equal(SourceState.Idle, testSource.State);

            VideoFrame? frame = await testSource.GetNextFrameAsync();
            Assert.Null(frame);

            await testSource.InitializeAsync();
            Assert.Equal(SourceState.Stopped, testSource.State);

            frame = await testSource.GetNextFrameAsync();
            Assert.Null(frame);
        }

        [Fact]
        public async void TestVideoSourceMultipleFrames()
        {
            MockVideoInput mockInput = new MockVideoInput();
            VideoSource testSource = new VideoSource("Test", mockInput);

            Assert.Equal(SourceState.Idle, testSource.State);

            await testSource.InitializeAsync();
            Assert.Equal(SourceState.Stopped, testSource.State);

            testSource.Start();
            Assert.Equal(SourceState.Running, testSource.State);

            for (int i = 0; i < 5; i++)
            {
                VideoFrame? frame = await testSource.GetNextFrameAsync();

                Assert.NotNull(frame);
                Assert.Equal(640, frame.Width);
                Assert.Equal(480, frame.Height);
                Assert.Equal(640 * 480 * 4, frame.Data.Length);
            }
        }
    }
}