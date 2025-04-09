using Kzone.Engine.Controller.Application.Interfaces;
using Kzone.Engine.Controller.Domain.Entities;
using Kzone.Engine.Controller.Domain.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kzone.Engine.Controller.Application.Services
{
    public class TorrentManagerService : ITorrentManagerService
    {
        private readonly ITorrentApi _torrentApi;

        public TorrentManagerService(ITorrentApi torrentApi)
        {
            _torrentApi = torrentApi;
        }

        //lấy danh sách torrent đang hoạt động trong engine
        public async Task<IEnumerable<Torrent>> GetTorrentsData(bool frozenCheck = false)
        {
            var torrents = await _torrentApi.GetTorrents(frozenCheck).ConfigureAwait(false);
            return torrents;
        }

        //start hoặc resume torrent
        public async Task StartTorrent(string hash)
        {
            await _torrentApi.StartTorrent(hash).ConfigureAwait(false);
        }

        //dừng tất cả hoạt động torrent
        public async Task StopTorrent(string hash)
        {
            await _torrentApi.StopTorrent(hash).ConfigureAwait(false);
        }

        //xoá torrent
        public async Task DeleteTorrent(string hash)
        {
            await _torrentApi.RemoveTorrent(hash).ConfigureAwait(false);
        }

        //thêm mới 1 torrent
        public async Task<bool> AddNewTorrent(string savePath, byte[] torrentBytes)
        {
            return await _torrentApi.AddTorrent(savePath, torrentBytes).ConfigureAwait(false);
        }


        public async Task UserAction(string hash, int actionType)
        {
            switch (actionType)
            {
                case 0:
                    await this.StartTorrent(hash).ConfigureAwait(false);
                    break;
                case 1:
                    await this.StopTorrent(hash).ConfigureAwait(false);
                    break;
                case 2:
                    await this.DeleteTorrent(hash).ConfigureAwait(false);
                    break;
                default:
                    break;
            }
        }
    }
}