using Kzone.Engine.Controller.Application.Interfaces;
using Kzone.Engine.Controller.Domain.Entities;
using KzoneSyncService.Application.Interfaces.Handlers;
using KzoneSyncService.Application.Interfaces.Services;
using KzoneSyncService.Domain.Entities;
using KzoneSyncService.Domain.Enums;
using NLog;
using System.Threading.Tasks;

namespace KzoneSyncService.Application.Handlers.TorrentHandles
{
    /// <summary>
    /// Handler xử lý thêm torrent mới vào engine
    /// </summary>
    public class NewTorrentHandler : BaseAddOrUpdateHandler, ITorrentHandler
    {
        public NewTorrentHandler(
            ITorrentManagerService torrentManager,
            IRdcCommunicateService rdcCommunicate,
            IBencodeParserService bencodeParser,
            ITorrentIssueService torrentIssue,
            ILogger logger)
            : base(torrentManager, rdcCommunicate, bencodeParser, torrentIssue, logger)
        {
        }

        public TorrentHandlerType HandleType => TorrentHandlerType.AddNew;

        /// <summary>
        /// Xử lý thêm mới torrent vào engine
        /// </summary>
        public async Task<TorrentOperationResult> Handle(TorrentPackageEntity package, Torrent engineTorrent)
        {
            return await ProcessTorrent(package, engineTorrent, $"{nameof(NewTorrentHandler)}");
        }
    }
}