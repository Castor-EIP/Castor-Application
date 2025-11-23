using FFMpegCore.Pipes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CastorCore.Frame
{
    public class AudioSample : IAudioSample
    {
        public byte[] Data { get; }
        public int Channels { get; }
        public int SampleRate { get; }
        public int SampleCount => Data.Length / Channels;

        public AudioSample(byte[] data, int channels, int sampleRate)
        {
            Data = data;
            Channels = channels;
            SampleRate = sampleRate;
        }

        public void Serialize(Stream stream)
        {
            stream.Write(Data, 0, Data.Length);
        }

        public async Task SerializeAsync(Stream stream, CancellationToken token)
        {
            await stream.WriteAsync(Data, token);
        }
    }
}
