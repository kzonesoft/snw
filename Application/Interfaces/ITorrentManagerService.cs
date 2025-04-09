using Kzone.Engine.Controller.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kzone.Engine.Controller.Application.Interfaces
{
    public interface ITorrentManagerService
    {
        Task<bool> AddNewTorrent(string savePath, byte[] torrentBytes);
        Task DeleteTorrent(string hash);
        Task<IEnumerable<Torrent>> GetTorrentsData(bool checkEngineFrozen = false);
        Task StartTorrent(string hash);
        Task StopTorrent(string hash);
        Task UserAction(string hash, int actionType);
    }
}