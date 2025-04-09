using System.Collections.Generic;

namespace Kzone.Engine.Controller.Domain.Entities
{
    public class TorrentCollection : List<Torrent>
    {
        public TorrentCollection() { }
        public TorrentCollection(IEnumerable<Torrent> collection) : base(collection) { }
        public TorrentCollection(int capacity) : base(capacity) { }
    }
}
