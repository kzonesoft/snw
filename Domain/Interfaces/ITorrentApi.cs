using Kzone.Engine.Controller.Domain.Entities;
using Kzone.Engine.Controller.Infrastructure.Api.Responses;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Kzone.Engine.Controller.Domain.Interfaces
{
    public interface ITorrentApi
    {
        void ConfigureAccess(int port, string userName, string password);
        Task<bool> IsApiAlive(CancellationToken token);
        Task<IEnumerable<Torrent>> GetTorrents(bool frozenCheck = false);
        Task<bool> AddTorrent(string savePath, byte[] torrentBytes);
        Task<Response> RemoveTorrent(string hash);
        Task<Response> RemoveTorrents(IEnumerable<string> hashs);
        Task<Response> StartTorrent(string hash);
        Task<Response> StartTorrents(IEnumerable<string> hashs);
        Task<Response> StopTorrent(string hash);
        Task<Response> StopTorrents(IEnumerable<string> hashs);
        Task<Response> GetSettings();
        Task<Response> SetSetting(string key, object value);
        Task<Response> SetSettings(Dictionary<string, object> settings);

    }
}