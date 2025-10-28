using CastorCore.Source.Frame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CastorCore.Input
{
    public interface IMediaInput<TFrame> where TFrame : MediaFrame
    {
        Task<bool> InitializeAsync(CancellationToken cancellationToken = default);
        Task<TFrame?> ReadFrameAsync(CancellationToken cancellationToken = default);
        void Dispose();
    }
}
