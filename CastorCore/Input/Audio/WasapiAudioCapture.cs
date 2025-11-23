using CastorCore.Frame;
using FFMpegCore.Pipes;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace CastorCore.Input.Audio
{
    public class WasapiAudioCapture : IAudioInput, IDisposable
    {
        private readonly MMDevice _device;
        private WasapiCapture _capture;

        private readonly ConcurrentQueue<IAudioSample> _queue = new();
        private volatile bool _isRecording;

        private int _channels;
        private int _sampleRate;

        public WasapiAudioCapture(MMDevice device)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
        }

        public void StartCapture()
        {
            if (_isRecording)
                return;

            _capture = new WasapiCapture(_device)
            {
                ShareMode = AudioClientShareMode.Shared
            };

            _channels = _capture.WaveFormat.Channels;
            _sampleRate = _capture.WaveFormat.SampleRate;

            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;

            _capture.StartRecording();
            _isRecording = true;
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            byte[] buffer = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, buffer, e.BytesRecorded);

            _queue.Enqueue(new AudioSample(buffer, _channels, _sampleRate));
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            _isRecording = false;
        }

        public void StopCapture()
        {
            if (!_isRecording)
                return;

            _capture?.StopRecording();
            _isRecording = false;
        }

        public IEnumerable<IAudioSample> PullSamples()
        {
            while (_isRecording || !_queue.IsEmpty)
            {
                if (_queue.TryDequeue(out var sample))
                {
                    yield return sample;
                }
                else
                {
                    Thread.Sleep(5); // Wait briefly if no samples are available
                }
            }
        }

        public void Dispose()
        {
            StopCapture();
            _capture?.Dispose();
        }
    }
}
