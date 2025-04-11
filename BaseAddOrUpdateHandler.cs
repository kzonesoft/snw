using Kzone.Engine.Controller.Application.Interfaces;
using Kzone.Engine.Controller.Domain.Entities;
using KzoneSyncService.Application.Interfaces.Services;
using KzoneSyncService.Domain.Entities;
using KzoneSyncService.Infrastructure.Extensions;
using NLog;
using System;
using System.Threading.Tasks;

namespace KzoneSyncService.Application.Handlers.TorrentHandles
{
    /// <summary>
    /// Lớp cơ sở chung cho các handler xử lý thêm mới và cập nhật torrent
    /// </summary>
    public abstract class BaseAddOrUpdateHandler
    {
        protected readonly ITorrentManagerService _torrentManager;
        protected readonly IRdcCommunicateService _rdcCommunicate;
        protected readonly IBencodeParserService _bencodeParser;
        protected readonly ITorrentIssueService _torrentIssue;
        protected readonly ILogger _logger;

        protected readonly string _binaryLockerKeyPrefix = "binary_";

        protected BaseAddOrUpdateHandler(
            ITorrentManagerService torrentManager,
            IRdcCommunicateService rdcCommunicate,
            IBencodeParserService bencodeParser,
            ITorrentIssueService torrentIssue,
            ILogger logger)
        {
            _torrentManager = torrentManager ?? throw new ArgumentNullException(nameof(torrentManager));
            _rdcCommunicate = rdcCommunicate ?? throw new ArgumentNullException(nameof(rdcCommunicate));
            _bencodeParser = bencodeParser ?? throw new ArgumentNullException(nameof(bencodeParser));
            _torrentIssue = torrentIssue ?? throw new ArgumentNullException(nameof(torrentIssue));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Xử lý chung cho việc thêm mới hoặc cập nhật torrent
        /// </summary>
        protected async Task<TorrentOperationResult> ProcessTorrent(
            TorrentPackageEntity package,
            Torrent engineTorrent,
            string operationName)
        {
            string lockerKey = $"{_binaryLockerKeyPrefix}{package.Hash}";

            try
            {
                _logger.Trace($"{operationName} process : {package.Name}");

                var torrentBinary = await _rdcCommunicate.DownloadTorrentBinary(package.Hash).ConfigureAwait(false);
                if (torrentBinary == null)
                {
                    _logger.Error($"Downloaded torrent binary is null for {operationName.ToLower()}: {package.Name}");
                    return TorrentOperationResult.Failed(package, "Torrent binary is null");
                }

                var savePath = package.GetServerPath();
                var torrentInfo = _bencodeParser.ParseBTorrentFromBytes(torrentBinary);

                if (torrentInfo == null)
                {
                    _logger.Error($"Torrent info is null for {operationName.ToLower()}: {package.Name}");
                    return TorrentOperationResult.Failed(package, "Torrent info is null");
                }

                if (torrentInfo.DisplayName != package.Name)
                {
                    _logger.Error($"Torrent name mismatch during {operationName.ToLower()}: {package.Name} != {torrentInfo.DisplayName}");
                    return TorrentOperationResult.Failed(package, "Torrent name mismatch");
                }

                if (torrentInfo.OriginalInfoHash != package.Hash)
                {
                    _logger.Trace($"Updating hash during {operationName.ToLower()} for {package.Name}: {package.Hash} -> {torrentInfo.OriginalInfoHash}");
                    package.Hash = torrentInfo.OriginalInfoHash;
                }

                // Xử lý trước khi download torrent (để lớp con override)
                await CleanUpOldData(package, engineTorrent);

                torrentInfo.CleanUnnecessaryFiles(savePath);

                bool result = await _torrentManager.AddNewTorrent(savePath, torrentBinary).ConfigureAwait(false);

                if (result)
                {
                    package.SetBeginDownloading();
                    _logger.Trace($"Successfully {operationName.ToLower()} torrent: {package.Name}");
                    return TorrentOperationResult.Succeeded(package);
                }
                else
                {
                    _torrentIssue.LockTorrent(lockerKey);
                    _logger.Error($"Failed to {operationName.ToLower()} torrent: {package.Name}");
                    return TorrentOperationResult.Failed(package, $"Failed to {operationName.ToLower()} torrent in engine");
                }
            }
            catch (Exception ex)
            {
                _torrentIssue.LockTorrent(lockerKey);
                _logger.Error($"Error {operationName.ToLower()}ing torrent [{package.Name}]: {ex.Message}");
                return TorrentOperationResult.Failed(package, ex.Message);
            }
        }

        /// <summary>
        /// Phương thức được gọi trước khi xử lý torrent, cho phép lớp con ghi đè để thêm logic tiền xử lý
        /// </summary>
        protected virtual Task CleanUpOldData(TorrentPackageEntity package, Torrent engineTorrent)
        {
#if !NET40
            return Task.CompletedTask;
#else
            return TaskExensions.Completed();
#endif
        }
    }
}