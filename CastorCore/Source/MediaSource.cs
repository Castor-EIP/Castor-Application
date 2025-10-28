using CastorCore.Input;
using CastorCore.Source.Frame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CastorCore.Source
{
    public abstract class MediaSource<TFrame> : IDisposable where TFrame : MediaFrame
    {
        private readonly IMediaInput<TFrame> _input;
        private SourceState _state = SourceState.Idle;
        private readonly object _stateLock = new object();
        private long _sequenceNumber;

        public string Name { get; }

        // Events
        public event EventHandler<TFrame>? FrameReady;
        public event EventHandler<Exception>? ErrorOccurred;

        public SourceState State
        {
            get { lock (_stateLock) return _state; }
            private set { lock (_stateLock) _state = value; }
        }

        protected MediaSource(string name, IMediaInput<TFrame> input)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _input = input ?? throw new ArgumentNullException(nameof(input));
        }

        public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (State != SourceState.Idle)
                throw new InvalidOperationException($"Cannot initialize from state: {State}");

            try
            {
                bool success = await _input.InitializeAsync(cancellationToken);
                if (success)
                    State = SourceState.Stopped;
                return success;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                return false;
            }
        }

        public void Start()
        {
            if (State != SourceState.Stopped && State != SourceState.Paused)
                throw new InvalidOperationException($"Cannot start from state: {State}");

            State = SourceState.Running;
        }

        public void Pause()
        {
            if (State != SourceState.Running)
                throw new InvalidOperationException($"Cannot pause from state: {State}");

            State = SourceState.Paused;
        }

        public void Stop()
        {
            if (State == SourceState.Stopped || State == SourceState.Idle)
                return;

            State = SourceState.Stopped;
        }

        public async Task<TFrame?> ReadFrameAsync(CancellationToken cancellationToken = default)
        {
            if (State != SourceState.Running)
                return null;

            try
            {
                TFrame? frame = await _input.ReadFrameAsync(cancellationToken);

                if (frame != null)
                {
                    frame.Timestamp = new MediaTimestamp
                    {
                        Ticks = DateTime.UtcNow.Ticks,
                        TimeSpan = DateTime.UtcNow.TimeOfDay,
                        FrameIndex = Interlocked.Increment(ref _sequenceNumber)
                    };

                    FrameReady?.Invoke(this, frame);
                }

                return frame;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                return null;
            }
        }

        public void Dispose()
        {
            Stop();
            _input?.Dispose();
        }
    }
}
