using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kzone.Engine.Controller.Infrastructure.Helpers
{
    /// <summary>
    /// Các phương thức mở rộng cho HttpClient.
    /// </summary>
    public static class HttpClientExtensions
    {
        /// <summary>
        /// Thiết lập thông tin xác thực cơ bản cho HttpClient.
        /// </summary>
        /// <param name="client">HttpClient instance</param>
        /// <param name="username">Tên đăng nhập</param>
        /// <param name="password">Mật khẩu</param>
        /// <returns>HttpClient với thông tin xác thực đã được thiết lập</returns>
        public static HttpClient WithBasicAuth(this HttpClient client, string username, string password)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                string authInfo = $"{username}:{password}";
                string base64 = Convert.ToBase64String(Encoding.ASCII.GetBytes(authInfo));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64);
            }

            return client;
        }

        /// <summary>
        /// Thiết lập timeout cho HttpClient.
        /// </summary>
        /// <param name="client">HttpClient instance</param>
        /// <param name="timeoutSeconds">Số giây trước khi timeout</param>
        /// <returns>HttpClient với timeout đã được thiết lập</returns>
        public static HttpClient WithTimeout(this HttpClient client, int timeoutSeconds)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            return client;
        }

        /// <summary>
        /// Gửi request và xử lý các ngoại lệ phổ biến.
        /// </summary>
        /// <typeparam name="T">Kiểu dữ liệu trả về</typeparam>
        /// <param name="client">HttpClient instance</param>
        /// <param name="requestMessage">HttpRequestMessage</param>
        /// <param name="cancellationToken">CancellationToken để hủy request</param>
        /// <param name="responseHandler">Hàm xử lý response thành kiểu T</param>
        /// <returns>Dữ liệu kiểu T hoặc default nếu có lỗi</returns>
        public static async Task<T> SendSafeAsync<T>(
            this HttpClient client,
            HttpRequestMessage requestMessage,
            CancellationToken cancellationToken,
            Func<HttpResponseMessage, Task<T>> responseHandler)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));
            if (requestMessage == null)
                throw new ArgumentNullException(nameof(requestMessage));
            if (responseHandler == null)
                throw new ArgumentNullException(nameof(responseHandler));

            try
            {
                using (var response = await client.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false))
                {
                    return await responseHandler(response).ConfigureAwait(false);
                }
            }
            catch (HttpRequestException)
            {
                return default;
            }
            catch (WebException)
            {
                return default;
            }
            catch (TaskCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException("Request was canceled", cancellationToken);
                }
                return default; // Timeout
            }
            catch (Exception)
            {
                return default;
            }
        }

        /// <summary>
        /// Đọc chuỗi từ response content.
        /// </summary>
        /// <param name="response">HttpResponseMessage</param>
        /// <returns>Chuỗi nội dung</returns>
        public static async Task<string> ReadContentAsStringAsync(this HttpResponseMessage response)
        {
            if (response == null)
                throw new ArgumentNullException(nameof(response));

            if (response.Content == null)
                return string.Empty;

            return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
    }
}