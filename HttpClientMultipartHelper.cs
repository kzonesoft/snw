using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Kzone.Engine.Controller.Infrastructure.Helpers
{
    /// <summary>
    /// Các phương thức tiện ích cho việc upload file và multipart form data với HttpClient
    /// </summary>
    public static class HttpClientMultipartHelper
    {
        /// <summary>
        /// Tạo một MultipartFormDataContent với boundary ngẫu nhiên
        /// </summary>
        /// <returns>MultipartFormDataContent instance</returns>
        public static MultipartFormDataContent CreateMultipartContent()
        {
            string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x", CultureInfo.InvariantCulture);
            return new MultipartFormDataContent(boundary);
        }

        /// <summary>
        /// Thêm file vào MultipartFormDataContent
        /// </summary>
        /// <param name="content">MultipartFormDataContent để thêm file</param>
        /// <param name="fileStream">Stream chứa dữ liệu file</param>
        /// <param name="fieldName">Tên trường form</param>
        /// <param name="fileName">Tên file</param>
        /// <param name="contentType">Kiểu MIME của file</param>
        /// <returns>MultipartFormDataContent đã được cập nhật</returns>
        public static MultipartFormDataContent AddFile(
            this MultipartFormDataContent content,
            Stream fileStream,
            string fieldName,
            string fileName,
            string contentType = "application/octet-stream")
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            if (fileStream == null)
                throw new ArgumentNullException(nameof(fileStream));
            if (string.IsNullOrEmpty(fieldName))
                throw new ArgumentException("Field name cannot be null or empty", nameof(fieldName));
            if (string.IsNullOrEmpty(fileName))
                throw new ArgumentException("File name cannot be null or empty", nameof(fileName));

            // Đảm bảo stream ở vị trí đầu
            fileStream.Position = 0;

            // Tạo StreamContent từ fileStream
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            // Thêm vào form data
            content.Add(streamContent, fieldName, fileName);

            return content;
        }

        /// <summary>
        /// Thêm một trường form bình thường vào MultipartFormDataContent
        /// </summary>
        /// <param name="content">MultipartFormDataContent để thêm trường</param>
        /// <param name="name">Tên trường</param>
        /// <param name="value">Giá trị của trường</param>
        /// <returns>MultipartFormDataContent đã được cập nhật</returns>
        public static MultipartFormDataContent AddField(
            this MultipartFormDataContent content,
            string name,
            string value)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Field name cannot be null or empty", nameof(name));

            // Tạo StringContent từ giá trị
            var stringContent = new StringContent(value ?? string.Empty);
            content.Add(stringContent, name);

            return content;
        }

        /// <summary>
        /// Gửi multipart request và xử lý phản hồi
        /// </summary>
        /// <typeparam name="T">Kiểu dữ liệu trả về</typeparam>
        /// <param name="client">HttpClient instance</param>
        /// <param name="requestUri">URI đích</param>
        /// <param name="content">MultipartFormDataContent chứa dữ liệu gửi đi</param>
        /// <param name="cancellationToken">Token để hủy thao tác</param>
        /// <param name="responseHandler">Hàm xử lý phản hồi</param>
        /// <returns>Dữ liệu kiểu T hoặc default nếu có lỗi</returns>
        public static async Task<T> PostMultipartAsync<T>(
            this HttpClient client,
            Uri requestUri,
            MultipartFormDataContent content,
            CancellationToken cancellationToken,
            Func<HttpResponseMessage, Task<T>> responseHandler)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));
            if (requestUri == null)
                throw new ArgumentNullException(nameof(requestUri));
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            if (responseHandler == null)
                throw new ArgumentNullException(nameof(responseHandler));

            try
            {
                using (var response = await client.PostAsync(requestUri, content, cancellationToken).ConfigureAwait(false))
                {
                    return await responseHandler(response).ConfigureAwait(false);
                }
            }
            catch (HttpRequestException)
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
    }
}