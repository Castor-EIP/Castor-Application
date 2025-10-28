using CastorCore.Input;
using CastorCore.Source.Frame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CastorCore.Source
{
    public class AudioSource : MediaSource<AudioFrame>
    {
        public AudioSource(string name, IAudioInput input) : base(name, input)
        {
        }
    }
}
