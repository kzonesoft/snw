using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Kzone.Signal.Server
{
    /// <summary>
    /// Quản lý các phiên (session) của identity.
    /// Việc khởi tạo session được bảo đảm thread-safe nhờ Lazy với ExecutionAndPublication.
    /// </summary>
    internal class IdentitySessions
    {
        // Sử dụng Lazy để khởi tạo lười, đảm bảo mỗi session chỉ được tạo một lần.
        private readonly ConcurrentDictionary<string, Lazy<SessionItem>> _sessions = new();

        /// <summary>
        /// Số lượng session hiện có.
        /// </summary>
        public int SessionCount => _sessions.Count;

        /// <summary>
        /// Lấy session theo sessionKey, nếu chưa có thì tạo mới bằng hàm createSession.
        /// </summary>
        public SessionItem ValidateOrAdd(string sessionKey, Func<SessionItem> createSession, Action<SessionItem> validateAction)
        {
            // Đảm bảo rằng session tồn tại, nếu không có thì tạo mới bằng createSession.
            var lazySession = _sessions.GetOrAdd(sessionKey, _ =>
                new Lazy<SessionItem>(
                    () => createSession(),
                    LazyThreadSafetyMode.ExecutionAndPublication)
            );

            // Lấy đối tượng session từ Lazy.
            var session = lazySession.Value;

            // Thực hiện validate dưới lock của session để đảm bảo an toàn trong môi trường đa luồng.
            lock (session.Lock)
            {
                validateAction(session);
            }

            return session;
        }


        /// <summary>
        /// Lấy session theo sessionKey nếu tồn tại.
        /// </summary>
        public SessionItem GetSession(string sessionKey)
        {
            if (_sessions.TryGetValue(sessionKey, out var lazySession))
            {
                return lazySession.Value;
            }
            return null;
        }

        /// <summary>
        /// Loại bỏ session theo sessionKey.
        /// </summary>
        public bool Remove(string sessionKey)
        {
            return _sessions.TryRemove(sessionKey, out _);
        }
    }

    /// <summary>
    /// Đại diện cho một phiên (session) của identity.
    /// Mọi thao tác cập nhật phải được thực hiện bên trong lock của đối tượng Lock.
    /// </summary>
    internal class SessionItem
    {
        /// <summary>
        /// Đối tượng Lock dùng để đồng bộ các thao tác cập nhật.
        /// </summary>
        public object Lock { get; } = new object();

        public Guid ConnectionId { get; set; }
        public string IdentityId { get; set; }
        public string IpPort { get; set; }
        public string IpOnly { get; set; }
        public DateTime Expires { get; set; }
        public DateTime ForcedSessionUpdate { get; set; }
    }
}
