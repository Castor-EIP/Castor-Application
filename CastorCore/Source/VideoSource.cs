using CastorCore.Input;
using CastorCore.Source.Frame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CastorCore.Source
{
    public class VideoSource : MediaSource<VideoFrame>
    {
        public VideoSource(string name, IVideoInput input) : base(name, input)
        {
        }
    }
}
