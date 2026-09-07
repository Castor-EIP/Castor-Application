using CastorApplication.Models.Studio;
using LibObs;

namespace CastorApplication.Services.Studio
{
    internal class ObsRecordingOutputFactory
    {
        private const string FfmpegOutputId = "ffmpeg_output";
        private const string LibVpxVp9EncoderName = "libvpx-vp9";
        private const string LibOpusEncoderName = "libopus";

        public static RecordingResources Create(RecordingRequest request)
        {
            return request.Container switch
            {
                RecordingContainer.Mkv => CreateMuxerOutput(request),
                RecordingContainer.Mp4 => CreateMuxerOutput(request),
                RecordingContainer.WebM => CreateWebMOutput(request),
                _ => throw new NotSupportedException($"Unsupported recording container: {request.Container}")
            };
        }

        private static RecordingResources CreateMuxerOutput(RecordingRequest request)
        {
            ObsEncoder? videoEncoder = null;
            ObsEncoder? audioEncoder = null;
            ObsOutput? output = null;

            try
            {
                using var videoSettings = new ObsData();

                videoSettings.SetString(ObsKnownSettings.Encoder.RateControl, "CBR");
                videoSettings.SetInt(ObsKnownSettings.Encoder.Bitrate, request.VideoBitrateKbps);

                videoSettings.SetInt(ObsKnownSettings.Encoder.KeyframeIntervalSeconds, 2);

                videoSettings.SetString(ObsKnownSettings.Encoder.Preset, "veryfast");

                videoEncoder = ObsEncoder.CreateVideo(
                    ObsKnownIds.Encoders.X264,
                    "castor-record-video",
                    videoSettings);

                videoEncoder.AttachToVideo();

                using var audioSettings = new ObsData();

                audioSettings.SetInt(ObsKnownSettings.Encoder.Bitrate, request.AudioBitrateKbps);

                audioEncoder = ObsEncoder.CreateAudio(
                    ObsKnownIds.Encoders.FfmpegAac,
                    "castor-record-audio",
                    settings: audioSettings);

                audioEncoder.AttachToAudio();

                using var outputSettings = new ObsData();

                outputSettings.SetString(
                    ObsKnownSettings.Output.Path,
                    request.OutputPath);

                outputSettings.SetString(
                    ObsKnownSettings.Output.MuxerSettings,
                    "");

                output = ObsOutput.Create(
                    ObsKnownIds.Outputs.FfmpegMuxer,
                    "castor-record-output",
                    outputSettings);

                output.SetVideoEncoder(videoEncoder);
                output.SetAudioEncoder(audioEncoder);

                return new RecordingResources(
                    output,
                    videoEncoder,
                    audioEncoder);
            }
            catch
            {
                output?.Dispose();
                audioEncoder?.Dispose();
                videoEncoder?.Dispose();

                throw;
            }
        }

        private static RecordingResources CreateWebMOutput(RecordingRequest request)
        {
            using var settings = new ObsData();

            settings.SetString("url", request.OutputPath);
            settings.SetString("format_name", "webm");
            settings.SetString("format_mime_type", "video/webm");

            settings.SetString(
                ObsKnownSettings.Output.MuxerSettings,
                "");

            settings.SetInt(
                "video_bitrate",
                request.VideoBitrateKbps);

            settings.SetInt(
                "audio_bitrate",
                request.AudioBitrateKbps);

            settings.SetInt(
                "gop_size",
                request.Fps * 2);

            settings.SetString(
                "video_encoder",
                LibVpxVp9EncoderName);

            settings.SetString(
                "audio_encoder",
                LibOpusEncoderName);

            settings.SetInt(
                "scale_width",
                request.OutputWidth);

            settings.SetInt(
                "scale_height",
                request.OutputHeight);

            ObsOutput? output = null;

            try
            {
                output = ObsOutput.Create(
                    FfmpegOutputId,
                    "castor-record-output",
                    settings);

                LibObsOutputInterop.SetAudioMixers(output, 1);

                return new RecordingResources(
                    output,
                    null,
                    null);
            }
            catch
            {
                output?.Dispose();
                throw;
            }
        }
    }
}
