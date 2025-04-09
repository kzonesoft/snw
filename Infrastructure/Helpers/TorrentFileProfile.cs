using System.Collections.Generic;

namespace Kzone.Engine.Controller.Infrastructure.Helpers
{
    public class TorrentFileProfile
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class TorrentFileProfileCollection : List<TorrentFileProfile> { }
}
