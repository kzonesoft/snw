using System;
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
        /// Tạo một HttpClient mới với cấu hình mặc định
        /// </summary>
        private static HttpClient CreateDefaultClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.Timeout = TimeSpan.FromSeconds(30);
            return client;
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

            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (!string.IsNullOrEmpty(baseUrl))
            {
                client.BaseAddress = new Uri(baseUrl);
            }

            client.Timeout = TimeSpan.FromSeconds(30);
            return client;
        }
    }
}