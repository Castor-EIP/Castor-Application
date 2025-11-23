using FFMpegCore.Pipes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CastorCore.Input
{
    public interface IAudioInput
    {
        void StartCapture();
        void StopCapture();

        IEnumerable<IAudioSample> PullSamples();
    }
}
