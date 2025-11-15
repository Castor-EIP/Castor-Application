using CastorCore.Input;
using FFMpegCore.Pipes;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CastorCore.Source
{
    public class VideoSource : ISource, IDisposable
    {
        private readonly IVideoInput _input;

        private readonly object _lock = new();
        private bool _running = false;
        private bool _disposed = false;
        private int _frameCount = 0;

        public event Action<IVideoFrame>? FrameReady;

        public VideoSource(IVideoInput input)
        {
            _input = input;
        }

        public async Task StartAsync(CancellationToken token)
        {
            lock (_lock)
            {
                if (_running)
                    throw new InvalidOperationException("VideoSource is already running.");

                _running = true;
            }

            DateTime startTime = DateTime.Now;

            while (!token.IsCancellationRequested)
            {
                IVideoFrame? frame = null;

                try
                {
                    frame = await _input.CaptureFrameAsync(token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    continue;
                }

                if (frame != null)
                {
                    _frameCount++;

                    if (_frameCount == 1)
                    {
                        TimeSpan elapsed = DateTime.Now - startTime;
                    }

                    // TODO: apply timestamp injection if needed
                    FrameReady?.Invoke(frame);
                }
                else
                {
                    // Pas de frame disponible, attendre un peu
                    await Task.Delay(1, token);
                }
            }

            lock (_lock)
            {
                _running = false;
            }
        }

        public async Task StopAsync(CancellationToken token)
        {
            lock (_lock)
            {
                if (!_running)
                    return;
            }

            await Task.Yield(); // give a point of cancellation
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _input?.Dispose();
        }
    }
}
