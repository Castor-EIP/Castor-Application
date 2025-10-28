using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CastorCore.Input;
using CastorCore.Source.Frame;

namespace CastorCoreTests.Mock
{
    public class MockVideoInput : IVideoInput
    {
        public Task<bool> InitializeAsync(CancellationToken ct = default)
        {
            return Task.FromResult(true);
        }

        public Task<VideoFrame?> ReadFrameAsync(CancellationToken ct = default)
        {
            return Task.FromResult<VideoFrame?>(new VideoFrame
            {
                Width = 640,
                Height = 480,
                Data = new byte[640 * 480 * 4]
            });
        }

        public void Dispose()
        {
        }
    }
}
