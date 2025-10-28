using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CastorCore.Source
{
    public class MediaTimestamp
    {
        public long Ticks { get; set; }
        public TimeSpan TimeSpan { get; set; }
        public long FrameIndex { get; set; }

        public static MediaTimestamp FromSeconds(double seconds, long frameIndex = 0)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);

            return new MediaTimestamp
            {
                Ticks = timeSpan.Ticks,
                TimeSpan = timeSpan,
                FrameIndex = frameIndex
            };
        }
    }
}
