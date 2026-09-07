using CastorApplication.Models.Settings;
using CastorApplication.Models.Studio;
using LibObs;

namespace CastorApplication.Services.Studio
{
    internal static class ObsMediaConfiguration
    {
        public static ObsVideoSettings CreatePreviewVideoSettings(ApplicationSettings settings)
        {
            var (baseWidth, baseHeight) =
                VideoResolution.BaseFromIndex(settings.SelectedBaseResolutionIndex);

            var (outputWidth, outputHeight) =
                VideoResolution.OutputFromIndex(settings.SelectedOutputResolutionIndex);

            var fps = settings.SelectedFpsIndex switch
            {
                0 => 60,
                2 => 25,
                _ => 30
            };

            return new ObsVideoSettings
            {
                FpsNumerator = (uint)fps,
                BaseWidth = (uint)baseWidth,
                BaseHeight = (uint)baseHeight,
                OutputWidth = (uint)outputWidth,
                OutputHeight = (uint)outputHeight
            };
        }

        public static ObsVideoSettings CreateRecordingVideoSettings(RecordingRequest request) =>
            new()
            {
                FpsNumerator = (uint)request.Fps,
                BaseWidth = (uint)request.BaseWidth,
                BaseHeight = (uint)request.BaseHeight,
                OutputWidth = (uint)request.OutputWidth,
                OutputHeight = (uint)request.OutputHeight,
                OutputFormat = ObsVideoFormat.Nv12,
                ColorSpace = ObsVideoColorSpace.Rec709,
                Range = ObsVideoRange.Partial,
                ScaleType = ObsScaleType.Bicubic
            };

        public static bool AreSameVideoSettings(
            ObsVideoSettings? left,
            ObsVideoSettings right) =>
            left != null &&
            left.FpsNumerator == right.FpsNumerator &&
            left.FpsDenominator == right.FpsDenominator &&
            left.BaseWidth == right.BaseWidth &&
            left.BaseHeight == right.BaseHeight &&
            left.OutputWidth == right.OutputWidth &&
            left.OutputHeight == right.OutputHeight;

        public static void ConfigureRecordingAudio(RecordingRequest request) =>
            Obs.ResetAudio(
                new ObsAudioSettings
                {
                    SamplesPerSecond = (uint)request.AudioSampleRate,
                    Speakers = request.AudioChannels == 1
                        ? ObsSpeakerLayout.Mono
                        : ObsSpeakerLayout.Stereo
                });
    }
}
