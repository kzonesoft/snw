using System;
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
        // Sử dụng Lazy<T> để đảm bảo thread-safe và chỉ khởi tạo một lần
        private static readonly Lazy<HttpClient> _clientLazy =
            new Lazy<HttpClient>(() => CreateDefaultClient(), LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// Lấy HttpClient instance mặc định đã được cấu hình
        /// </summary>
        public static HttpClient Client => _clientLazy.Value;

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
            ServicePointManager.DefaultConnectionLimit = 20;

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
        /// Tạo HttpClient mới với URL cơ sở và thông tin xác thực
        /// </summary>
        /// <param name="baseUrl">URL cơ sở</param>
        /// <param name="username">Tên đăng nhập</param>
        /// <param name="password">Mật khẩu</param>
        /// <returns>HttpClient đã được cấu hình</returns>
        public static HttpClient CreateClient(string baseUrl, string username, string password)
        {
            var handler = new HttpClientHandler();

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                handler.Credentials = new System.Net.NetworkCredential(username, password);
            }

            // Cấu hình ServicePoint nếu chưa
            ConfigureServicePointManager();

            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Cấu hình keep-alive
            client.DefaultRequestHeaders.ConnectionClose = false;
            client.DefaultRequestHeaders.Connection.Add("keep-alive");

            if (!string.IsNullOrEmpty(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl);

                // Cấu hình ServicePoint cho URI cụ thể này
                ServicePoint servicePoint = ServicePointManager.FindServicePoint(new Uri(baseUrl));
                servicePoint.ConnectionLimit = 20;
                servicePoint.ConnectionLeaseTimeout = 60000; // 60 giây
            }

            client.Timeout = TimeSpan.FromSeconds(30);
            return client;
        }
    }
}