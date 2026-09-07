using CastorApplication.Models.Studio;
using LibObs;

namespace CastorApplication.Services.Studio
{
    internal static class ObsSourceCatalog
    {
        public static SourceCatalog Enumerate(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var videos = new List<CaptureSourceOption>();
            var audio = new List<AudioSourceOption>();
            var failures = new List<string>();

            TryEnumerate("monitors", () => EnumerateMonitors(videos), failures);
            TryEnumerate("windows", () => EnumerateWindows(videos), failures);
            TryEnumerate("cameras", () => EnumerateCameras(videos), failures);

            TryEnumerate("audio-systems", () => EnumerateSystemAudio(audio), failures);
            TryEnumerate("microphones", () => EnumerateMicrophones(audio), failures);

            ct.ThrowIfCancellationRequested();

            return new SourceCatalog(videos, audio, string.Join(" | ", failures));
        }

        private static void EnumerateMonitors(List<CaptureSourceOption> videos)
        {
            var monitors = ObsSource
                .GetWindowsDisplayCaptureTargets()
                .Select(device =>
                    new CaptureSourceOption(
                        device.Id,
                        device.DisplayName,
                        VideoCaptureKind.Monitor,
                        device.Id));

            videos.AddRange(monitors);
        }

        private static void EnumerateWindows(List<CaptureSourceOption> videos)
        {
            var windows = ObsSource
                .GetPropertyListItems(
                    ObsKnownIds.Sources.WindowsWindowCapture,
                    ObsKnownSettings.WindowsWindowCapture.Window)
                .Select(window =>
                    new CaptureSourceOption(
                        window.Value,
                        window.DisplayName,
                        VideoCaptureKind.Window,
                        window.Value));

            videos.AddRange(windows);
        }

        private static void EnumerateCameras(List<CaptureSourceOption> videos)
        {
            var cameras = ObsSource
                .GetPropertyListItems(
                    ObsKnownIds.Sources.WindowsVideoCaptureDevice,
                    ObsKnownSettings.WindowsVideoCaptureDevice.VideoDeviceId)
                .Select(device =>
                    new CaptureSourceOption(
                        device.Value,
                        device.DisplayName,
                        VideoCaptureKind.Camera,
                        device.Value));

            videos.AddRange(cameras);
        }

        private static void EnumerateSystemAudio(List<AudioSourceOption> audio)
        {
            var audioSystems = ObsSource
                .GetPropertyListItems(
                    ObsKnownIds.Sources.WindowsAudioOutputCapture,
                    ObsKnownSettings.WindowsAudioCapture.DeviceId)
                .Select(device =>
                    new AudioSourceOption(
                        device.Value,
                        device.DisplayName,
                        AudioCaptureKind.LoopbackGlobal,
                        device.Value));

            audio.AddRange(audioSystems);
        }

        private static void EnumerateMicrophones(List<AudioSourceOption> audio)
        {
            var mics = ObsSource
                .GetPropertyListItems(
                    ObsKnownIds.Sources.WindowsAudioInputCapture,
                    ObsKnownSettings.WindowsAudioCapture.DeviceId)
                .Select(device =>
                    new AudioSourceOption(
                        device.Value,
                        device.DisplayName,
                        AudioCaptureKind.Microphone,
                        device.Value));

            audio.AddRange(mics);
        }

        private static void TryEnumerate(string category, Action enumerate, ICollection<string> failures)
        {
            try
            {
                enumerate();
            }
            catch (Exception exception)
            {
                failures.Add($"Unable to enumerate {category} : {exception.Message}");
            }
        }
    }
}
