using Kzone.Engine.Controller.Infrastructure.Caching;
using System;

namespace Kzone.Engine.Controller.Application.Services
{
    /// <summary>
    /// Thông tin về quá trình sửa chữa torrent
    /// </summary>
    internal class TorrentRepairInfo
    {
        /// <summary>
        /// Số lần đã thử sửa chữa
        /// </summary>
        internal int RepairCount { get; set; }

        /// <summary>
        /// Thời điểm sửa chữa gần nhất
        /// </summary>
        internal DateTime LastRepairTime { get; set; }
    }

    /// <summary>
    /// Lớp quản lý cache cho thông tin sửa chữa torrent
    /// </summary>
    internal class TorrentRepairCache : ExpiringCache<TorrentRepairInfo>
    {
        /// <summary>
        /// Khởi tạo cache cho thông tin sửa chữa torrent với thời gian hết hạn mặc định là 5 giờ
        /// </summary>
        internal TorrentRepairCache() : base(TimeSpan.FromHours(5))
        {
        }

        /// <summary>
        /// Khởi tạo cache cho thông tin sửa chữa torrent với thời gian hết hạn tùy chỉnh
        /// </summary>
        /// <param name="expirationTime">Thời gian hết hạn cho thông tin sửa chữa</param>
        internal TorrentRepairCache(TimeSpan expirationTime) : base(expirationTime)
        {
        }

        /// <summary>
        /// Kiểm tra nếu cần sửa chữa torrent và cập nhật thông tin
        /// </summary>
        /// <param name="torrentId">ID của torrent</param>
        /// <param name="maxRepairCount">Số lần sửa chữa tối đa (mặc định: 5)</param>
        /// <param name="repairInterval">Khoảng thời gian giữa các lần sửa chữa (mặc định: 5 phút)</param>
        /// <returns>true nếu torrent nên được sửa chữa</returns>
        internal bool NeedsRepair(string torrentId, int maxRepairCount = 5, TimeSpan? repairInterval = null)
        {
            if (string.IsNullOrEmpty(torrentId)) return false;

            var interval = repairInterval ?? TimeSpan.FromMinutes(5);

            if (TryGetValue(torrentId, out var info))
            {
                // Chỉ sửa chữa nếu đã đủ thời gian từ lần cuối và chưa vượt giới hạn
                if (DateTime.Now > info.LastRepairTime.Add(interval) && info.RepairCount < maxRepairCount)
                {
                    info.RepairCount++;
                    info.LastRepairTime = DateTime.Now;
                    Update(torrentId, info);
                    return true;
                }
                return false;
            }
            else
            {
                // Lần sửa chữa đầu tiên
                Add(torrentId, new TorrentRepairInfo
                {
                    RepairCount = 1,
                    LastRepairTime = DateTime.Now
                });
                return true;
            }
        }

        /// <summary>
        /// Lấy số lần sửa chữa hiện tại của torrent
        /// </summary>
        /// <param name="torrentId">ID của torrent</param>
        /// <returns>Số lần sửa chữa, hoặc 0 nếu không tìm thấy</returns>
        internal int GetRepairCount(string torrentId)
        {
            if (string.IsNullOrEmpty(torrentId)) return 0;

            if (TryGetValue(torrentId, out var info))
            {
                return info.RepairCount;
            }

            return 0;
        }

        /// <summary>
        /// Đặt lại thông tin sửa chữa cho torrent (đặt số lần sửa chữa về 0)
        /// </summary>
        /// <param name="torrentId">ID của torrent</param>
        /// <returns>true nếu thành công, false nếu không</returns>
        internal bool ResetRepairInfo(string torrentId)
        {
            if (string.IsNullOrEmpty(torrentId)) return false;

            if (TryGetValue(torrentId, out var info))
            {
                info.RepairCount = 0;
                info.LastRepairTime = DateTime.Now;
                return Update(torrentId, info);
            }

            return false;
        }

        /// <summary>
        /// Xóa thông tin sửa chữa của torrent
        /// </summary>
        /// <param name="torrentId">ID của torrent</param>
        /// <returns>true nếu xóa thành công, false nếu không</returns>
        internal bool RemoveRepairInfo(string torrentId)
        {
            if (string.IsNullOrEmpty(torrentId)) return false;
            return Remove(torrentId);
        }

        /// <summary>
        /// Xóa thông tin sửa chữa của tất cả các torrent đã vượt quá số lần sửa chữa cho phép
        /// </summary>
        /// <param name="maxRepairCount">Số lần sửa chữa tối đa</param>
        /// <returns>Số lượng torrent đã xóa</returns>
        internal int CleanupMaxedOutRepairs(int maxRepairCount)
        {
            var allRepairInfo = GetAllWithKeys();
            int removedCount = 0;

            foreach (var pair in allRepairInfo)
            {
                if (pair.Value.RepairCount >= maxRepairCount)
                {
                    if (Remove(pair.Key))
                    {
                        removedCount++;
                    }
                }
            }

            return removedCount;
        }
    }
}