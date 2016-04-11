using System;
using System.Collections.Generic;
using System.Text;

namespace Shared
{
    public class Destination
    {
        public string partner { get; set; }
        public string location { get; set; }
    }

    public class Rendition
    {
        public string ffmpeg { get; set; }
        public string suffix { get; set; }
        public List<Destination> destinations { get; set; }
    }

    public class ProfileDefinition
    {
        public string name { get; set; }
        public List<Rendition> renditions { get; set; }
    }
}
