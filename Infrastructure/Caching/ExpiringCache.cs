using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Kzone.Engine.Controller.Infrastructure.Caching
{
    /// <summary>
    /// Lớp ExpiringCache tối ưu quản lý cache với hết hạn tuyệt đối
    /// Dữ liệu sẽ hết hạn tại thời điểm cố định, không phụ thuộc vào thời gian truy cập
    /// </summary>
    /// <typeparam name="T">Kiểu dữ liệu của giá trị cache</typeparam>
    internal abstract class ExpiringCache<T> : IDisposable
    {
        private class CacheItem
        {
            public T Value { get; set; }
            public DateTime ExpiresAt { get; }

            public CacheItem(T value, DateTime expiresAt)
            {
                Value = value;
                ExpiresAt = expiresAt;
            }

            public bool IsExpired => DateTime.UtcNow > ExpiresAt;
        }

        private readonly ConcurrentDictionary<string, CacheItem> _cache;
        private readonly TimeSpan _defaultExpirationTime;
        private bool _disposed;

        /// <summary>
        /// Khởi tạo một instance mới của ExpiringCache
        /// </summary>
        /// <param name="defaultExpirationTime">Thời gian hết hạn mặc định (mặc định 30 phút)</param>
        internal ExpiringCache(TimeSpan? defaultExpirationTime = null)
        {
            _cache = new ConcurrentDictionary<string, CacheItem>(StringComparer.Ordinal);
            _defaultExpirationTime = defaultExpirationTime ?? TimeSpan.FromMinutes(30);
        }

        /// <summary>
        /// Thêm một item mới vào cache
        /// </summary>
        /// <param name="key">Khóa của item</param>
        /// <param name="value">Giá trị cần lưu trữ</param>
        /// <param name="expirationTime">Thời gian hết hạn tuyệt đối (sử dụng giá trị mặc định nếu null)</param>
        /// <returns>true nếu thêm thành công, false nếu khóa đã tồn tại</returns>
        internal bool Add(string key, T value, TimeSpan? expirationTime = null)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            var expTime = expirationTime ?? _defaultExpirationTime;
            var expiresAt = DateTime.UtcNow.Add(expTime);
            var item = new CacheItem(value, expiresAt);

            return _cache.TryAdd(key, item);
        }

        /// <summary>
        /// Thêm hoặc cập nhật một item trong cache
        /// </summary>
        /// <param name="key">Khóa của item</param>
        /// <param name="value">Giá trị cần lưu trữ</param>
        /// <param name="expirationTime">Thời gian hết hạn tuyệt đối (sử dụng giá trị mặc định nếu null)</param>
        /// <returns>true nếu thêm mới, false nếu cập nhật</returns>
        internal bool AddOrUpdate(string key, T value, TimeSpan? expirationTime = null)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            var expTime = expirationTime ?? _defaultExpirationTime;
            var expiresAt = DateTime.UtcNow.Add(expTime);
            var item = new CacheItem(value, expiresAt);

            var isNew = !_cache.ContainsKey(key);
            _cache.AddOrUpdate(key, item, (_, __) => item);

            return isNew;
        }

        /// <summary>
        /// Lấy giá trị item từ cache theo khóa
        /// </summary>
        /// <param name="key">Khóa của item</param>
        /// <returns>Giá trị của item</returns>
        /// <exception cref="KeyNotFoundException">Ném ra nếu khóa không tồn tại hoặc item đã hết hạn</exception>
        internal T Get(string key)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            if (_cache.TryGetValue(key, out var item))
            {
                if (item.IsExpired)
                {
                    _cache.TryRemove(key, out _);
                    throw new KeyNotFoundException($"Item với khóa '{key}' đã hết hạn");
                }

                return item.Value;
            }

            throw new KeyNotFoundException($"Không tìm thấy item với khóa '{key}'");
        }

        /// <summary>
        /// Thử lấy giá trị item từ cache theo khóa
        /// </summary>
        /// <param name="key">Khóa của item</param>
        /// <param name="value">Biến out để lưu giá trị nếu tìm thấy</param>
        /// <returns>true nếu tìm thấy item và chưa hết hạn, false nếu không</returns>
        internal bool TryGetValue(string key, out T value)
        {
            value = default;

            if (string.IsNullOrEmpty(key)) return false;

            if (_cache.TryGetValue(key, out var item))
            {
                if (item.IsExpired)
                {
                    _cache.TryRemove(key, out _);
                    return false;
                }

                value = item.Value;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Kiểm tra xem một khóa có tồn tại trong cache không
        /// </summary>
        /// <param name="key">Khóa cần kiểm tra</param>
        /// <returns>true nếu khóa tồn tại và chưa hết hạn, false nếu không</returns>
        internal bool ContainsKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;

            if (_cache.TryGetValue(key, out var item))
            {
                if (item.IsExpired)
                {
                    _cache.TryRemove(key, out _);
                    return false;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Cập nhật giá trị một item trong cache
        /// </summary>
        /// <param name="key">Khóa của item</param>
        /// <param name="value">Giá trị mới</param>
        /// <returns>true nếu cập nhật thành công, false nếu item không tồn tại hoặc đã hết hạn</returns>
        internal bool Update(string key, T value)
        {
            if (string.IsNullOrEmpty(key)) return false;

            if (_cache.TryGetValue(key, out var existingItem))
            {
                if (existingItem.IsExpired)
                {
                    _cache.TryRemove(key, out _);
                    return false;
                }

                // Giữ nguyên thời gian hết hạn, chỉ cập nhật giá trị
                var newItem = new CacheItem(value, existingItem.ExpiresAt);
                return _cache.TryUpdate(key, newItem, existingItem);
            }

            return false;
        }

        /// <summary>
        /// Cập nhật giá trị một item trong cache thông qua action
        /// </summary>
        /// <param name="key">Khóa của item</param>
        /// <param name="updateAction">Action để cập nhật giá trị</param>
        /// <returns>true nếu cập nhật thành công, false nếu item không tồn tại hoặc đã hết hạn</returns>
        internal bool Update(string key, Action<T> updateAction)
        {
            if (string.IsNullOrEmpty(key) || updateAction == null) return false;

            if (_cache.TryGetValue(key, out var existingItem))
            {
                if (existingItem.IsExpired)
                {
                    _cache.TryRemove(key, out _);
                    return false;
                }

                // Cập nhật giá trị thông qua action
                updateAction(existingItem.Value);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Xóa một item khỏi cache theo khóa
        /// </summary>
        /// <param name="key">Khóa của item cần xóa</param>
        /// <returns>true nếu xóa thành công, false nếu khóa không tồn tại</returns>
        internal bool Remove(string key) =>
            !string.IsNullOrEmpty(key) && _cache.TryRemove(key, out _);

        /// <summary>
        /// Xóa tất cả các item trong cache
        /// </summary>
        internal void Clear() => _cache.Clear();

        /// <summary>
        /// Lấy tất cả các giá trị chưa hết hạn từ cache
        /// </summary>
        /// <returns>Danh sách các giá trị chưa hết hạn</returns>
        internal List<T> GetAll()
        {
            var result = new List<T>();
            var expiredKeys = new List<string>();

            foreach (var pair in _cache)
            {
                if (pair.Value.IsExpired)
                {
                    expiredKeys.Add(pair.Key);
                }
                else
                {
                    result.Add(pair.Value.Value);
                }
            }

            // Xóa các key đã hết hạn
            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }

            return result;
        }

        /// <summary>
        /// Lấy tất cả các item cùng với khóa từ cache
        /// </summary>
        /// <returns>Dictionary chứa các cặp khóa-giá trị chưa hết hạn</returns>
        internal Dictionary<string, T> GetAllWithKeys()
        {
            var result = new Dictionary<string, T>();
            var expiredKeys = new List<string>();

            foreach (var pair in _cache)
            {
                if (pair.Value.IsExpired)
                {
                    expiredKeys.Add(pair.Key);
                }
                else
                {
                    result[pair.Key] = pair.Value.Value;
                }
            }

            // Xóa các key đã hết hạn
            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }

            return result;
        }

        /// <summary>
        /// Lấy thời gian hết hạn của item trong cache
        /// </summary>
        /// <param name="key">Khóa của item</param>
        /// <param name="expiresAt">Thời gian hết hạn</param>
        /// <returns>true nếu item tồn tại và chưa hết hạn, false nếu không</returns>
        internal bool GetExpirationTime(string key, out DateTime expiresAt)
        {
            expiresAt = DateTime.MinValue;

            if (string.IsNullOrEmpty(key)) return false;

            if (_cache.TryGetValue(key, out var item))
            {
                if (item.IsExpired)
                {
                    _cache.TryRemove(key, out _);
                    return false;
                }

                expiresAt = item.ExpiresAt;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Lấy số lượng item hiện có trong cache (không bao gồm các item đã hết hạn)
        /// </summary>
        internal int Count
        {
            get
            {
                var count = 0;
                var expiredKeys = new List<string>();

                foreach (var pair in _cache)
                {
                    if (pair.Value.IsExpired)
                    {
                        expiredKeys.Add(pair.Key);
                    }
                    else
                    {
                        count++;
                    }
                }

                // Xóa các key đã hết hạn
                foreach (var key in expiredKeys)
                {
                    _cache.TryRemove(key, out _);
                }

                return count;
            }
        }

        /// <summary>
        /// Dọn dẹp các item đã hết hạn trong cache
        /// </summary>
        /// <returns>Số lượng item đã được dọn dẹp</returns>
        internal int CleanupExpiredItems()
        {
            var expiredKeys = new List<string>();

            foreach (var pair in _cache)
            {
                if (pair.Value.IsExpired)
                {
                    expiredKeys.Add(pair.Key);
                }
            }

            // Xóa các key đã hết hạn
            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }

            return expiredKeys.Count;
        }

        /// <summary>
        /// Giải phóng tài nguyên
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _cache.Clear();
            _disposed = true;
        }
    }
}