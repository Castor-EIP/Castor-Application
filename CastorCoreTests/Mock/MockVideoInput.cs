using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CastorCore.Input;
using FFMpegCore.Pipes;

namespace CastorCoreTests.Mock
{
    public class MockVideoInput : IVideoInput
    {
        private readonly int _frameDelay;
        private int _framesCaptured;

        public int Width { get; }
        public int Height { get; }
        public string Format { get; } = "bgra32";

        /// <summary>
        /// Number of frames captured by this input
        /// </summary>
        public int FramesCaptured => _framesCaptured;

        public MockVideoInput(int width = 640, int height = 480, int frameDelay = 0)
        {
            Width = width;
            Height = height;
            _frameDelay = frameDelay;
        }

        public async Task<IVideoFrame?> CaptureFrameAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return null;

            // Simulate frame capture delay if specified
            if (_frameDelay > 0)
            {
                await Task.Delay(_frameDelay, token);
            }

            // Create a test frame with dummy data
            MockVideoFrame frame = new MockVideoFrame(Width, Height);
            
            _framesCaptured++;
            
            return frame;
        }

        public void Dispose()
        {
            // Nothing to dispose in mock
        }
    }
}
