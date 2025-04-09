using Kzone.Engine.Controller.Infrastructure.Caching;
using System;

namespace Kzone.Engine.Controller.Application.Services
{
    /// <summary>
    /// Lớp quản lý cache cho các torrent đang bị khóa
    /// </summary>
    internal class LockedTorrentsCache : ExpiringCache<object>
    {
        /// <summary>
        /// Khởi tạo cache cho các torrent bị khóa với thời gian hết hạn là 30 giây
        /// </summary>
        internal LockedTorrentsCache() : base(TimeSpan.FromSeconds(30))
        {
        }

        /// <summary>
        /// Khóa một torrent trong khoảng thời gian mặc định (30 giây)
        /// </summary>
        /// <param name="torrentHash">Hash của torrent</param>
        /// <returns>true nếu thêm mới, false nếu cập nhật</returns>
        internal bool LockTorrent(string torrentHash)
        {
            if (string.IsNullOrEmpty(torrentHash)) return false;
            return AddOrUpdate(torrentHash, new object());
        }

        /// <summary>
        /// Khóa một torrent với thời gian tùy chỉnh
        /// </summary>
        /// <param name="torrentHash">Hash của torrent</param>
        /// <param name="lockDuration">Thời gian khóa</param>
        /// <returns>true nếu thêm mới, false nếu cập nhật</returns>
        internal bool LockTorrent(string torrentHash, TimeSpan lockDuration)
        {
            if (string.IsNullOrEmpty(torrentHash)) return false;
            return AddOrUpdate(torrentHash, new object(), lockDuration);
        }

        /// <summary>
        /// Kiểm tra xem một torrent có đang bị khóa hay không
        /// </summary>
        /// <param name="torrentHash">Hash của torrent</param>
        /// <returns>true nếu torrent đang bị khóa</returns>
        internal bool IsLocked(string torrentHash)
        {
            if (string.IsNullOrEmpty(torrentHash)) return false;
            return ContainsKey(torrentHash);
        }

        /// <summary>
        /// Mở khóa một torrent
        /// </summary>
        /// <param name="torrentHash">Hash của torrent</param>
        /// <returns>true nếu mở khóa thành công, false nếu không</returns>
        internal bool UnlockTorrent(string torrentHash)
        {
            if (string.IsNullOrEmpty(torrentHash)) return false;
            return Remove(torrentHash);
        }

        /// <summary>
        /// Lấy số lượng torrent đang bị khóa
        /// </summary>
        /// <returns>Số lượng torrent đang bị khóa</returns>
        internal int GetLockedTorrentsCount()
        {
            return Count;
        }

        /// <summary>
        /// Mở khóa tất cả các torrent
        /// </summary>
        internal void UnlockAllTorrents()
        {
            Clear();
        }
    }
}