using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;

namespace Kzone.Engine.Controller.Infrastructure.Helpers
{
    /// <summary>
    /// Factory để quản lý và tạo các HttpClient instances.
    /// Thiết kế theo singleton pattern để tối ưu việc sử dụng tài nguyên.
    /// </summary>
    public static class HttpClientFactory
    {
        // Singleton instance cho mỗi base URL và cấu hình
        private static readonly ConcurrentDictionary<string, Lazy<HttpClient>> _clientCache =
            new ConcurrentDictionary<string, Lazy<HttpClient>>();

        // Client mặc định
        private static readonly Lazy<HttpClient> _defaultClientLazy =
            new Lazy<HttpClient>(() => CreateDefaultClient(), LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// Lấy HttpClient instance mặc định đã được cấu hình
        /// </summary>
        public static HttpClient Client => _defaultClientLazy.Value;

        /// <summary>
        /// Lấy hoặc tạo một HttpClient dựa trên URL cơ sở và thông tin xác thực
        /// </summary>
        /// <param name="baseUrl">URL cơ sở</param>
        /// <param name="username">Tên đăng nhập (tùy chọn)</param>
        /// <param name="password">Mật khẩu (tùy chọn)</param>
        /// <param name="cookieContainer">Container chứa cookies (tùy chọn)</param>
        /// <returns>HttpClient đã được cấu hình</returns>
        public static HttpClient GetOrCreateClient(string baseUrl, string username = null, string password = null, CookieContainer cookieContainer = null)
        {
            // Tạo key duy nhất cho cấu hình này
            string key = GenerateClientKey(baseUrl, username, password);

            return _clientCache.GetOrAdd(key, _ =>
                new Lazy<HttpClient>(() => CreateConfiguredClient(baseUrl, username, password, cookieContainer),
                    LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        }

        /// <summary>
        /// Tạo key duy nhất cho cấu hình HttpClient
        /// </summary>
        private static string GenerateClientKey(string baseUrl, string username, string password)
        {
            return $"{baseUrl ?? "default"}|{username ?? ""}|{password?.GetHashCode() ?? 0}";
        }

        /// <summary>
        /// Tạo một HttpClient mới với cấu hình được chỉ định
        /// </summary>
        private static HttpClient CreateConfiguredClient(string baseUrl, string username, string password, CookieContainer cookieContainer)
        {
            var handler = new HttpClientHandler();

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                handler.Credentials = new NetworkCredential(username, password);
            }

            if (cookieContainer != null)
            {
                handler.CookieContainer = cookieContainer;
            }
            else
            {
                // Khởi tạo cookie container mặc định
                handler.CookieContainer = new CookieContainer();
            }

            // Cấu hình ServicePoint để tối ưu connection pooling
            ConfigureServicePointManager();

            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.ConnectionClose = false; // Giữ kết nối mở
            client.DefaultRequestHeaders.Connection.Add("keep-alive");

            if (!string.IsNullOrEmpty(baseUrl))
            {
                Uri baseUri = new Uri(baseUrl);
                client.BaseAddress = baseUri;

                // Cấu hình ServicePoint cho URI cụ thể này
                ServicePoint servicePoint = ServicePointManager.FindServicePoint(baseUri);
                servicePoint.ConnectionLimit = 20;
                servicePoint.ConnectionLeaseTimeout = 60000; // 60 giây
            }

            client.Timeout = TimeSpan.FromSeconds(30);
            return client;
        }

        /// <summary>
        /// Tạo một HttpClient mới với cấu hình mặc định và keepalive
        /// </summary>
        private static HttpClient CreateDefaultClient()
        {
            var handler = new HttpClientHandler();

            // Cấu hình ServicePoint để tối ưu connection pooling
            ConfigureServicePointManager();

            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Cấu hình keep-alive
            client.DefaultRequestHeaders.ConnectionClose = false; // Giữ kết nối mở
            client.DefaultRequestHeaders.Connection.Add("keep-alive");

            // Timeout 30 giây
            client.Timeout = TimeSpan.FromSeconds(30);

            return client;
        }

        /// <summary>
        /// Cấu hình ServicePointManager để tối ưu connection pooling và giảm TIME_WAIT
        /// </summary>
        private static void ConfigureServicePointManager()
        {
            // Tăng số lượng kết nối đồng thời đến một host
            ServicePointManager.DefaultConnectionLimit = 15;

            // Cấu hình keep-alive toàn cục
            ServicePointManager.SetTcpKeepAlive(true, 30000, 10000);  // Bật keepalive, sử dụng probe mỗi 30 giây, với interval 10 giây

            // Thiết lập thời gian tối đa mà một kết nối có thể được tái sử dụng
            ServicePointManager.MaxServicePointIdleTime = 60000; // 60 giây

            // Tắt cơ chế Expect: 100-continue để giảm độ trễ của request đầu tiên
            ServicePointManager.Expect100Continue = false;

            // Thiết lập thời gian một connection nằm trong connection pool
            ServicePointManager.MaxServicePointIdleTime = 300000; // 5 phút
        }

        /// <summary>
        /// Xóa tất cả các HttpClient trong cache
        /// </summary>
        public static void ClearClientCache()
        {
            _clientCache.Clear();
        }
    }
}