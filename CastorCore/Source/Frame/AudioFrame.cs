using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CastorCore.Source.Frame
{
    public class AudioFrame : MediaFrame
    {
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public float[] Samples { get; set; }

        public TimeSpan Duration =>
            TimeSpan.FromSeconds((double)Samples.Length / (SampleRate * Channels));

        public override void Dispose()
        {
            if (!_disposed)
            {
                Samples = null;
                _disposed = true;
            }
        }
    }
}
