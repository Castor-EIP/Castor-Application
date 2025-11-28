using FFMpegCore.Pipes;

namespace CastorCore.Source
{
    public interface IAudioSource
    {
        IEnumerator<IAudioSample> GetAudioSamples();
        RawAudioPipeSource ToPipeSource();
    }
}