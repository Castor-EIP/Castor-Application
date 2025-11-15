using CastorCore.Input;
using FFMpegCore.Pipes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CastorCore.Source
{
    public interface ISource
    {
        public event Action<IVideoFrame>? FrameReady;

        public Task StartAsync(CancellationToken token);
        public Task StopAsync(CancellationToken token);
    }
}
