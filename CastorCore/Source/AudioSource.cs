using CastorCore.Input;
using FFMpegCore.Pipes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CastorCore.Source
{
    public class AudioSource : IAudioSource
    {
        private readonly IAudioInput _input;

        public AudioSource(IAudioInput input)
        {
            _input = input;
        }

        public IEnumerator<IAudioSample> GetAudioSamples()
        {
            foreach (IAudioSample sample in _input.PullSamples())
            {
                yield return sample;
            }
        }
    }
}
