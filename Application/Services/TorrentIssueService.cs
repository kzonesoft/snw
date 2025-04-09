using Kzone.Engine.Controller.Application.Interfaces;
using System;

namespace Kzone.Engine.Controller.Application.Services
{
    /// <summary>
    /// Lớp quản lý các torrent, bao gồm trạng thái khóa và giám sát lỗi
    /// </summary>
    public class TorrentIssueService : IDisposable, ITorrentIssueService
    {
        private readonly LockedTorrentsCache _lockedTorrents;
        private readonly TorrentRepairCache _repairTracker;

        /// <summary>
        /// Khởi tạo TorrentIssueManager với thời gian hết hạn mặc định
        /// </summary>
        public TorrentIssueService()
        {
            _lockedTorrents = new LockedTorrentsCache();
            _repairTracker = new TorrentRepairCache();
        }


        #region Khóa torrent

        /// <summary>
        /// Khóa một torrent
        /// </summary>
        /// <param name="torrentHash">Hash của torrent</param>
        public void LockTorrent(string torrentHash)
        {
            if (string.IsNullOrEmpty(torrentHash)) return;
            _lockedTorrents.LockTorrent(torrentHash);
        }

        /// <summary>
        /// Khóa một torrent với thời gian tùy chỉnh
        /// </summary>
        /// <param name="torrentHash">Hash của torrent</param>
        /// <param name="lockDuration">Thời gian khóa</param>
        public void LockTorrent(string torrentHash, TimeSpan lockDuration)
        {
            if (string.IsNullOrEmpty(torrentHash)) return;
            _lockedTorrents.LockTorrent(torrentHash, lockDuration);
        }

        /// <summary>
        /// Kiểm tra xem một torrent có đang bị khóa hay không
        /// </summary>
        /// <param name="torrentHash">Hash của torrent</param>
        /// <returns>true nếu torrent đang bị khóa</returns>
        public bool IsLocked(string torrentHash)
        {
            if (string.IsNullOrEmpty(torrentHash)) return false;
            return _lockedTorrents.IsLocked(torrentHash);
        }

        /// <summary>
        /// Mở khóa một torrent
        /// </summary>
        /// <param name="torrentHash">Hash của torrent</param>
        public void UnlockTorrent(string torrentHash)
        {
            if (string.IsNullOrEmpty(torrentHash)) return;
            _lockedTorrents.UnlockTorrent(torrentHash);
        }

        /// <summary>
        /// Lấy số lượng torrent đang bị khóa
        /// </summary>
        /// <returns>Số lượng torrent đang bị khóa</returns>
        public int GetLockedTorrentsCount()
        {
            return _lockedTorrents.GetLockedTorrentsCount();
        }

        /// <summary>
        /// Mở khóa tất cả các torrent
        /// </summary>
        public void UnlockAllTorrents()
        {
            _lockedTorrents.UnlockAllTorrents();
        }
        #endregion

        #region Giám sát và sửa chữa

        /// <summary>
        /// Kiểm tra nếu cần sửa chữa torrent và cập nhật thông tin
        /// </summary>
        /// <param name="torrentId">ID của torrent</param>
        /// <param name="maxRepairCount">Số lần sửa chữa tối đa (mặc định: 5)</param>
        /// <param name="repairInterval">Khoảng thời gian giữa các lần sửa chữa (mặc định: 5 phút)</param>
        /// <returns>true nếu torrent nên được sửa chữa</returns>
        public bool NeedsRepair(string torrentId, int maxRepairCount = 5, TimeSpan? repairInterval = null)
        {
            if (string.IsNullOrEmpty(torrentId)) return false;
            return _repairTracker.NeedsRepair(torrentId, maxRepairCount, repairInterval);
        }

        /// <summary>
        /// Lấy số lần sửa chữa hiện tại của torrent
        /// </summary>
        /// <param name="torrentId">ID của torrent</param>
        /// <returns>Số lần sửa chữa, hoặc 0 nếu không tìm thấy</returns>
        public int GetRepairCount(string torrentId)
        {
            if (string.IsNullOrEmpty(torrentId)) return 0;
            return _repairTracker.GetRepairCount(torrentId);
        }

        /// <summary>
        /// Đặt lại thông tin sửa chữa cho torrent (đặt số lần sửa chữa về 0)
        /// </summary>
        /// <param name="torrentId">ID của torrent</param>
        /// <returns>true nếu thành công, false nếu không</returns>
        public bool ResetRepairInfo(string torrentId)
        {
            if (string.IsNullOrEmpty(torrentId)) return false;
            return _repairTracker.ResetRepairInfo(torrentId);
        }

        /// <summary>
        /// Xóa thông tin sửa chữa của torrent
        /// </summary>
        /// <param name="torrentId">ID của torrent</param>
        public void RemoveRepairInfo(string torrentId)
        {
            if (string.IsNullOrEmpty(torrentId)) return;
            _repairTracker.RemoveRepairInfo(torrentId);
        }

        /// <summary>
        /// Xóa thông tin sửa chữa của tất cả các torrent đã vượt quá số lần sửa chữa cho phép
        /// </summary>
        /// <param name="maxRepairCount">Số lần sửa chữa tối đa</param>
        /// <returns>Số lượng torrent đã xóa</returns>
        public int CleanupMaxedOutRepairs(int maxRepairCount)
        {
            return _repairTracker.CleanupMaxedOutRepairs(maxRepairCount);
        }
        #endregion



        /// <summary>
        /// Giải phóng tài nguyên
        /// </summary>
        public void Dispose()
        {
            _lockedTorrents?.Dispose();
            _repairTracker?.Dispose();
        }
    }
}