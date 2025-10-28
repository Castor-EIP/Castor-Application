using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CastorCore.Source.Frame
{
    public abstract class MediaFrame : IDisposable
    {
        protected bool _disposed;

        public MediaTimestamp Timestamp { get; set; } = new MediaTimestamp();
        public long SequenceNumber { get; set; }
        public bool IsEndOfStream { get; set; }

        public abstract void Dispose();
    }
}
