using AutoMapper;
using Kzone.Engine.Controller.Application.Interfaces;
using Kzone.Engine.Controller.Domain.Entities;
using Kzone.ViewDto;
using KzoneSyncService.Application.Interfaces.AppSessions;
using KzoneSyncService.Application.Interfaces.Handlers;
using KzoneSyncService.Application.Interfaces.Repositories;
using KzoneSyncService.Application.Interfaces.Services;
using KzoneSyncService.Domain.Entities;
using KzoneSyncService.Domain.Enums;
using KzoneSyncService.Infrastructure.AppSessions.Sessions;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace KzoneSyncService.Application.Services
{
    /// <summary>
    /// Service chính để đồng bộ giữa engine và cơ sở dữ liệu
    /// </summary>
    public class EngineSyncService : IEngineSyncService
    {
        private readonly ITorrentRepository _repository;
        private readonly IMapper _mapper;
        private readonly ISessionStorage _sessionStorage;
        private readonly ITorrentIssueService _torrentIssue;
        private readonly ITorrentManagerService _torrentManager;
        private readonly ILogger _logger;
        private readonly IEnumerable<ITorrentHandler> _torrentHandlers;

        // Đặt là biến cục bộ thay vì trường đối tượng để tránh vấn đề về đồng bộ
        // giữa các lần gọi Sync khác nhau
        private int _currentDownload;

        public EngineSyncService(
            ITorrentRepository repository,
            IMapper mapper,
            ISessionStorage sessionStorage,
            ITorrentIssueService torrentIssue,
            ITorrentManagerService torrentManager,
            IEnumerable<ITorrentHandler> torrentHandlers,
            ILogger logger
           )
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _sessionStorage = sessionStorage ?? throw new ArgumentNullException(nameof(sessionStorage));
            _torrentIssue = torrentIssue ?? throw new ArgumentNullException(nameof(torrentIssue));
            _torrentManager = torrentManager ?? throw new ArgumentNullException(nameof(torrentManager));
            _torrentHandlers = torrentHandlers ?? throw new ArgumentNullException(nameof(torrentHandlers));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Đồng bộ dữ liệu giữa engine và database
        /// </summary>
        public async Task Sync()
        {
            var torrentSession = _sessionStorage.TorrentPackageSession;

            // Kiểm tra dữ liệu có hợp lệ không
            if (torrentSession == null || torrentSession.LastExecuteSuccess.AddMinutes(10) < DateTime.Now)
            {
                _logger.Trace("Data outdated, wait until rdc-sync. Skip this job");
                return;
            }

            _logger.Trace("Begin engine data synchronization");

            try
            {
                // Danh sách chứa các torrent thay đổi cần cập nhật vào database
                var changedTorrents = new List<TorrentPackageEntity>();

                // Lấy dữ liệu từ database và engine
                var databaseTorrents = _repository.GetDownloadedPackages()?.ToList() ?? new List<TorrentPackageEntity>();
                var engineTorrents = await _repository.GetEngineTorrents(true) ?? new Torrent[0];

                // Cập nhật số lượng torrent đang tải
                _currentDownload = _repository.GetCurrentDownloadCount(engineTorrents);
                var maxDownloadActive = _repository.GetMaxDownloadActive();

                _logger.Trace($"Current download: {_currentDownload}, Max allowed: {maxDownloadActive}");
                _logger.Trace($"Found {engineTorrents.Count()} torrents in engine and {databaseTorrents.Count} torrents in database");

                // Xây dựng dictionaries để tìm kiếm nhanh hơn
                var engineTorrentDict = engineTorrents
                    .Where(t => t != null && !string.IsNullOrEmpty(t.Name))
                    .ToDictionary(
                        t => t.Name.ToLowerInvariant(),
                        t => t);

                var dbTorrentDict = databaseTorrents
                    .Where(t => t != null && !string.IsNullOrEmpty(t.Name))
                    .ToDictionary(
                        t => t.Name.ToLowerInvariant(),
                        t => t);

                // PHẦN 1: Xử lý torrent từ Engine - xóa nếu không có trong DB hoặc đánh dấu hoàn thành
                await ProcessEngineTorrents(engineTorrentDict, dbTorrentDict, changedTorrents);

                // PHẦN 2: Xử lý torrent từ Database - thêm mới vào engine nếu chưa có
                // Chỉ xử lý các torrent CHƯA được thay đổi ở bước trước
                var processedNames = new HashSet<string>(changedTorrents.Select(t => t.Name.ToLowerInvariant()));
                await ProcessDatabaseTorrents(dbTorrentDict, engineTorrentDict, processedNames, maxDownloadActive, changedTorrents);

                // Cập nhật thay đổi vào database
                if (changedTorrents.Count > 0)
                {
                    _repository.UpdateTorrentChanges(changedTorrents);
                    _logger.Trace($"Engine synchronization completed. Updated {changedTorrents.Count} torrents.");
                }
                else
                {
                    _logger.Trace("Engine synchronization completed. No torrents were changed.");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error during engine synchronization: {ex.Message}");
                _logger.Error(ex.StackTrace);
            }
        }

        /// <summary>
        /// Xử lý các torrent hiện có trong engine
        /// </summary>
        private async Task ProcessEngineTorrents(
            Dictionary<string, Torrent> engineTorrents,
            Dictionary<string, TorrentPackageEntity> databaseTorrents,
            List<TorrentPackageEntity> changedTorrents)
        {
            if (engineTorrents == null || !engineTorrents.Any())
            {
                _logger.Trace("No engine torrents found to process");
                return;
            }

            foreach (var kvp in engineTorrents)
            {
                var torrentName = kvp.Key;
                var engineTorrent = kvp.Value;

                try
                {
                    // Cập nhật dữ liệu GUI cho tất cả các torrent đang tải
                    if (engineTorrent.Progress < 1000)
                    {
                        var engineDataDto = _mapper.Map<EngineDataDto>(engineTorrent);
                        _sessionStorage.EngineDataStore.AddOrUpdate(engineTorrent.Name, engineDataDto);
                    }

                    // Kiểm tra torrent có tồn tại trong database không
                    if (!databaseTorrents.TryGetValue(torrentName, out var databaseTorrent))
                    {
                        // TRƯỜNG HỢP 1: Torrent không tồn tại trong database, xóa khỏi engine
                        _logger.Trace($"Torrent {engineTorrent.Name} not found in database. Deleting from engine.");
                        try
                        {
                            var deleteHandler = GetHandler(TorrentHandlerType.Delete);
                            await deleteHandler.Handle(null, engineTorrent);
                            _logger.Trace($"Successfully deleted torrent {engineTorrent.Name} from engine.");
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"Failed to delete torrent {engineTorrent.Name}: {ex.Message}");
                        }
                        continue;
                    }

                    // TRƯỜNG HỢP 2: Torrent bị disable, bỏ qua
                    if (!databaseTorrent.Enable)
                    {
                        _logger.Trace($"Torrent {engineTorrent.Name} is disabled. Skipping.");
                        continue;
                    }

                    // TRƯỜNG HỢP 3: Torrent đã tải hoàn tất (100%), đánh dấu hoàn thành
                    if (engineTorrent.Progress == 1000 &&
                          databaseTorrent.DownloadFinish == DateTime.MinValue &&
                          IsHashMatch(databaseTorrent.Hash, engineTorrent.Hash))
                    {
                        _logger.Trace($"Torrent {engineTorrent.Name} completed. Marking as finished.");
                        try
                        {
                            var completeHandler = GetHandler(TorrentHandlerType.Complete);
                            var result = await completeHandler.Handle(databaseTorrent, engineTorrent);
                            if (result.Success && result.Package != null)
                            {
                                changedTorrents.Add(result.Package);
                                _logger.Trace($"Successfully marked torrent {engineTorrent.Name} as completed.");
                            }
                            else
                            {
                                _logger.Warn($"Failed to mark torrent {engineTorrent.Name} as completed: {result.Message}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"Error marking torrent {engineTorrent.Name} as completed: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error processing engine torrent [{engineTorrent?.Name ?? "unknown"}]: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Xử lý các torrent từ database
        /// </summary>
        private async Task ProcessDatabaseTorrents(
            Dictionary<string, TorrentPackageEntity> databaseTorrents,
            Dictionary<string, Torrent> engineTorrents,
            HashSet<string> processedTorrents,
            int maxDownloadActive,
            List<TorrentPackageEntity> changedTorrents)
        {
            if (databaseTorrents == null || !databaseTorrents.Any())
            {
                _logger.Trace("No database torrents found to process");
                return;
            }

            // Lọc danh sách torrent cần xử lý - các torrent active chưa tải xong
            var torrentsToProcess = databaseTorrents.Values
                .Where(t => t.Enable &&
                       t.DownloadFinish == DateTime.MinValue &&
                       !string.IsNullOrEmpty(t.Hash) &&
                       !processedTorrents.Contains(t.Name.ToLowerInvariant()))
                .ToList();

            _logger.Trace($"Found {torrentsToProcess.Count} active torrents to process from database");

            foreach (var databaseTorrent in torrentsToProcess)
            {
                if (_currentDownload >= maxDownloadActive)
                {
                    _logger.Trace($"Reached download limit: {_currentDownload} >= {maxDownloadActive}. Stopping process.");
                    break;
                }

                string lockerKey = $"binary_{databaseTorrent.Hash}";
                if (_torrentIssue.IsLocked(lockerKey))
                {
                    _logger.Trace($"Skipping locked torrent: {databaseTorrent.Name}");
                    continue;
                }

                var torrentNameLower = databaseTorrent.Name.ToLowerInvariant();
                var engineTorrentExists = engineTorrents.TryGetValue(torrentNameLower, out var engineTorrent);

                try
                {
                    // Trường hợp 1: Torrent không tồn tại trong engine, thêm mới
                    if (!engineTorrentExists)
                    {
                        _logger.Trace($"Adding new torrent to engine: {databaseTorrent.Name}");
                        try
                        {
                            var addNewHandler = GetHandler(TorrentHandlerType.AddNew);
                            var result = await addNewHandler.Handle(databaseTorrent, null);
                            if (result.Success)
                            {
                                _currentDownload++;
                                if (result.Package != null)
                                {
                                    changedTorrents.Add(result.Package);
                                    _logger.Trace($"Successfully added new torrent {databaseTorrent.Name} to engine.");
                                }
                            }
                            else
                            {
                                _logger.Warn($"Failed to add new torrent {databaseTorrent.Name}: {result.Message}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"Error adding new torrent {databaseTorrent.Name}: {ex.Message}");
                        }
                        continue;
                    }

                    // Trường hợp 2: Torrent tồn tại nhưng hash không khớp, cập nhật
                    if (!IsHashMatch(engineTorrent.Hash, databaseTorrent.Hash))
                    {
                        _logger.Trace($"Updating torrent in engine: {databaseTorrent.Name} (Hash mismatch: {engineTorrent.Hash} != {databaseTorrent.Hash})");
                        try
                        {
                            var updateHandler = GetHandler(TorrentHandlerType.Update);
                            var result = await updateHandler.Handle(databaseTorrent, engineTorrent);
                            if (result.Success && result.Package != null)
                            {
                                changedTorrents.Add(result.Package);
                                _logger.Trace($"Successfully updated torrent {databaseTorrent.Name} in engine.");
                            }
                            else
                            {
                                _logger.Warn($"Failed to update torrent {databaseTorrent.Name}: {result.Message}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"Error updating torrent {databaseTorrent.Name}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error processing database torrent [{databaseTorrent.Name}]: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Kiểm tra và sửa chữa các torrent lỗi
        /// </summary>
        public async Task CheckAndRepairTorrent()
        {
            _logger.Trace("Checking for torrents needing repair");

            try
            {
                var torrentsWithErrors = _sessionStorage.EngineDataStore.GetAll()
                    ?.Where(x => x != null && x.Status == "Error" && x.Progress < 100)
                    ?.ToArray() ?? new EngineDataDto[0];

                _logger.Trace($"Found {torrentsWithErrors.Length} torrents with error status");

                foreach (var torrent in torrentsWithErrors)
                {
                    if (torrent == null || string.IsNullOrEmpty(torrent.Name))
                    {
                        _logger.Warn("Found null or invalid torrent in error list, skipping");
                        continue;
                    }

                    try
                    {
                        if (_torrentIssue.NeedsRepair(torrent.Name))
                        {
                            _logger.Trace($"Repairing torrent: {torrent.Name} - hash: {torrent.Hash}");

                            if (string.IsNullOrEmpty(torrent.Hash))
                            {
                                _logger.Warn($"Cannot repair torrent {torrent.Name} - hash is empty");
                                continue;
                            }

                            await _torrentManager.DeleteTorrent(torrent.Hash).ConfigureAwait(false);
                            _logger.Trace($"Add to repair queue: {torrent.Name}");
                        }
                    }
                    catch (Exception innerEx)
                    {
                        _logger.Error($"Error repairing torrent {torrent.Name}: {innerEx.Message}");
                    }
                }

                _logger.Trace("Torrent repair check completed");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error checking torrents for repair: {ex.Message}");
            }
        }

        /// <summary>
        /// Lấy handler phù hợp dựa vào loại xử lý
        /// </summary>
        private ITorrentHandler GetHandler(TorrentHandlerType torrentHandle)
        {
            return _torrentHandlers.FirstOrDefault(x => x.HandleType == torrentHandle);
        }

        /// <summary>
        /// Kiểm tra xem hai hash có khớp nhau không, đảm bảo xử lý các trường hợp null và trống
        /// </summary>
        /// <param name="hash1">Hash thứ nhất cần so sánh</param>
        /// <param name="hash2">Hash thứ hai cần so sánh</param>
        /// <returns>True nếu hai hash khớp nhau, False nếu không khớp hoặc một trong hai giá trị null/trống</returns>
        private bool IsHashMatch(string hash1, string hash2)
        {
            if (string.IsNullOrEmpty(hash1) || string.IsNullOrEmpty(hash2))
            {
                return false;
            }

            return hash1.Equals(hash2, StringComparison.OrdinalIgnoreCase);
        }
    }
}