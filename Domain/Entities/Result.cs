using Kzone.Engine.Controller.Domain.Exceptions;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net;

namespace Kzone.Engine.Controller.Domain.Entities
{
    public class Result
    {
        public JObject Source { get; }
        public HttpStatusCode StatusCode { get; set; }
        public int Build { get; set; }
        public TorrentException Error { get; set; }
        public int CacheId { get; set; }

        public IList<Label> Label { get; } = new List<Label>();

        public IList<string> Messages { get; } = new List<string>();

        public IList<Torrent> Torrents { get; } = new List<Torrent>();

        public IList<Torrent> ChangedTorrents { get; } = new List<Torrent>();

        public IList<object> RssFilters { get; } = new List<object>();

        public IDictionary<string, FileCollection> Files { get; } = new Dictionary<string, FileCollection>();

        public List<Setting> Settings { get; } = new List<Setting>();

        public List<Props> Props { get; } = new List<Props>();

        public Result(JObject source)
        {
            Source = source;
        }
    }
}
