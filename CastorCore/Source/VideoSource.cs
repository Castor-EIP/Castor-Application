using CastorCore.Input;
using FFMpegCore.Pipes;

namespace CastorCore.Source
{
    public class VideoSource : IVideoSource
    {
        private readonly IVideoInput _input;
        private IVideoFrame? _currentFrame;

        private readonly SemaphoreSlim _lock = new(1);
        private CancellationTokenSource? _captureCts;
        private Task? _captureTask;

        public int Width => _input.Width;
        public int Height => _input.Height;
        public string Name { get; init; }

        public VideoSource(IVideoInput input, string name)
        {
            _input = input;
            Name = name;
        }

        public void Start()
        {
            _captureCts = new CancellationTokenSource();
            _captureTask = Task.Run(async () =>
            {
                while (!_captureCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        IVideoFrame? frame = await _input.CaptureFrameAsync(_captureCts.Token);
                        if (frame != null)
                        {
                            await _lock.WaitAsync(_captureCts.Token);

                            try
                            {
                                _currentFrame = frame;
                            }
                            finally
                            {
                                _lock.Release();
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {

                    }
                }
            });
        }

        public async Task<IVideoFrame?> GetFrameAsync(CancellationToken token)
        {
            await _lock.WaitAsync(token);

            try
            {
                return _currentFrame;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async void Stop()
        {
            _captureCts?.Cancel();
            if (_captureTask != null)
                await _captureTask;
        }

        public void Dispose()
        {
            Stop();
            _input?.Dispose();
            _lock?.Dispose();
            _captureCts?.Dispose();
        }
    }
}
