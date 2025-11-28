using CastorCore.Input;
using FFMpegCore.Pipes;
using NAudio.Wave;
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

        public RawAudioPipeSource ToPipeSource()
        {
            WaveFormat waveFormat = _input.Device.AudioClient.MixFormat;

            RawAudioPipeSource rawAudioPipeSource = new RawAudioPipeSource(GetAudioSamples())
            {
                Channels = (uint)waveFormat.Channels,
                SampleRate = (uint)waveFormat.SampleRate,
            };

            return rawAudioPipeSource;
        }
    }
}
