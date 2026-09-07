using CastorApplication.Models.Studio;
using LibObs;

namespace CastorApplication.Services.Studio
{
    internal class ObsSourceFactory
    {
        public static ObsSource Create(SourceAddRequest request) => request switch
        {
            SourceAddRequest.Video video => CreateVideoSource(video),
            SourceAddRequest.Audio audio => CreateAudioSource(audio),
            SourceAddRequest.Media media => CreateMediaSource(media),
            _ => throw new NotSupportedException($"Unsupported source type: {request}")
        };

        private static ObsSource CreateVideoSource(SourceAddRequest.Video request)
        {
            return request.Option.Type switch {
                VideoCaptureKind.Monitor => CreateMonitorSource(request),
                VideoCaptureKind.Window => CreateWindowSource(request),
                VideoCaptureKind.Camera => CreateCameraSource(request),
                _ => throw new NotSupportedException($"Unsupported video source type: {request.Option.Type}")
            };
        }

        private static ObsSource CreateMonitorSource(SourceAddRequest.Video request)
        {
            var settings = new ObsWindowsDisplayCaptureSettings
            {
                MonitorId = request.Option.Id
            };

            return ObsSource.CreateWindowsDisplayCapture(request.RequestedName, settings);
        }

        private static ObsSource CreateWindowSource(SourceAddRequest.Video request)
        {
            var settings = new ObsWindowsWindowCaptureSettings
            {
                Window = request.Option.Id
            };

            return ObsSource.CreateWindowsWindowCapture(request.RequestedName, settings);
        }

        private static ObsSource CreateCameraSource(SourceAddRequest.Video request)
        {
            var settings = new ObsWindowsVideoCaptureDeviceSettings
            {
                DeviceId = request.Option.Id
            };

            return ObsSource.CreateWindowsVideoCaptureDevice(request.RequestedName, settings);
        }

        private static ObsSource CreateAudioSource(SourceAddRequest.Audio request)
        {
            return request.Option.Type switch
            {
                AudioCaptureKind.LoopbackWindow => CreateLoopbackWindowSource(request),
                AudioCaptureKind.CameraMic => CreateCameraMicSource(request),
                _ => throw new NotSupportedException($"Unsupported audio source type: {request.Option.Type}")
            };
        }

        private static ObsSource CreateLoopbackWindowSource(SourceAddRequest.Audio request)
        {
            var settings = new ObsWindowsAudioCaptureSettings
            {
                DeviceId = request.Option.Id
            };

            return ObsSource.CreateWindowsAudioOutputCapture(request.RequestedName, settings);
        }

        private static ObsSource CreateCameraMicSource(SourceAddRequest.Audio request)
        {
            var settings = new ObsWindowsAudioCaptureSettings
            {
                DeviceId = request.Option.Id
            };

            return ObsSource.CreateWindowsAudioInputCapture(request.RequestedName, settings);
        }

        private static ObsSource CreateMediaSource(SourceAddRequest.Media request)
        {
            var settings = new ObsMediaSourceSettings
            {
                FilePath = request.FilePath,
                Loop = request.Loop
            };

            return ObsSource.CreateMediaSource(request.RequestedName, settings);
        }
    }
}
