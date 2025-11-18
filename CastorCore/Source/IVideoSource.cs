using CastorCore.Input;
using FFMpegCore.Pipes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CastorCore.Source
{
    public interface IVideoSource : IDisposable
    {
        int Width { get; }
        int Height { get; }

        Task<IVideoFrame?> GetFrameAsync(CancellationToken token);
    }
}
