using Kzone.Engine.Controller.Domain.Entities;
using Kzone.Engine.Controller.Infrastructure.Api.Responses;
using Kzone.Engine.Controller.Infrastructure.Helpers;
using System;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Kzone.Engine.Controller.Infrastructure.Api.Requests
{
    /// <summary>
    /// Request để thêm torrent từ dữ liệu byte array.
    /// Tối ưu hóa để không cần chuyển đổi giữa stream và byte array không cần thiết.
    /// </summary>
    public class AddByteArrayRequest : BaseAddRequest<AddByteArrayResponse>, IDisposable
    {
        #region Properties

        private byte[] _fileBytes;
        public byte[] FileBytes => _fileBytes;

        private TorrentInfo _torrentInfo;
        public TorrentInfo TorrentInfo => _torrentInfo;

        #endregion

        #region Fluent Setter

        /// <summary>
        /// Thiết lập dữ liệu torrent từ mảng byte
        /// </summary>
        /// <param name="fileBytes">Mảng byte chứa dữ liệu tệp torrent</param>
        /// <returns>Instance hiện tại của AddByteArrayRequest</returns>
        public AddByteArrayRequest SetFile(byte[] fileBytes)
        {
            if (fileBytes == null)
                throw new ArgumentNullException(nameof(fileBytes));
            if (fileBytes.Length == 0)
                throw new ArgumentException("File data cannot be empty", nameof(fileBytes));

            _fileBytes = fileBytes;

            // Phân tích dữ liệu torrent để lấy thông tin
            using (var stream = new MemoryStream(fileBytes))
            {
                var result = BencodeDecoder.Decode(stream);

                if (result == null || result.Length == 0 || !(result[0] is BDictionary))
                {
                    throw new InvalidOperationException("Invalid torrent data");
                }

                _torrentInfo = TorrentInfo.Parse((BDictionary)result[0]);
            }

            return this;
        }

        #endregion

        protected override void ToUrl(StringBuilder sb)
        {
            base.ToUrl(sb);

            if (UrlAction == UrlAction.AddFile && _fileBytes == null)
                throw new InvalidOperationException("Torrent data is missing with AddFile action");
        }

        protected override void OnProcessingRequest(HttpClient httpClient, HttpRequestMessage requestMessage)
        {
            if (_fileBytes != null)
            {
                // Chuyển từ GET sang POST
                requestMessage.Method = HttpMethod.Post;

                // Tạo boundary giống với SampleOK
                string boundary = "----WebKitFormBoundary" + DateTime.Now.Ticks.ToString("x", CultureInfo.InvariantCulture);

                // Header
                string headerTemplate =
                    "--" + boundary + "\r\n" +
                    "Content-Disposition: form-data; name=\"torrent_file\"; filename=\"file.torrent\"\r\n" +
                    "Content-Type: application/x-bittorrent\r\n\r\n";

                // Footer
                string footerTemplate = "\r\n--" + boundary + "--\r\n";

                // Tính toán kích thước tổng cộng
                int totalLength = Encoding.UTF8.GetByteCount(headerTemplate) + _fileBytes.Length + Encoding.UTF8.GetByteCount(footerTemplate);

                // Tạo một mảng byte duy nhất chứa tất cả dữ liệu
                byte[] multipartContent = new byte[totalLength];
                int offset = 0;

                // Ghi header
                byte[] headerBytes = Encoding.UTF8.GetBytes(headerTemplate);
                Buffer.BlockCopy(headerBytes, 0, multipartContent, offset, headerBytes.Length);
                offset += headerBytes.Length;

                // Ghi dữ liệu file
                Buffer.BlockCopy(_fileBytes, 0, multipartContent, offset, _fileBytes.Length);
                offset += _fileBytes.Length;

                // Ghi footer
                byte[] footerBytes = Encoding.UTF8.GetBytes(footerTemplate);
                Buffer.BlockCopy(footerBytes, 0, multipartContent, offset, footerBytes.Length);

                // Tạo content từ byte array
                var content = new ByteArrayContent(multipartContent);
                content.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data; boundary=" + boundary);

                // Gán nội dung cho request
                requestMessage.Content = content;
            }
        }

        protected override void OnProcessedRequest(AddByteArrayResponse result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            base.OnProcessedRequest(result);
            result.AddedTorrentInfo = _torrentInfo;
        }

        protected override bool CheckAction(UrlAction action)
        {
            return action == UrlAction.AddFile;
        }

        protected override Torrent FindAddedTorrent(AddByteArrayResponse result)
        {
            if (result == null || result.Result?.Torrents == null)
                return null;

            if (_torrentInfo == null || string.IsNullOrEmpty(_torrentInfo.Hash))
            {
                // Nếu không có hash, tìm theo tên như trước
                if (_torrentInfo?.Name == null)
                    return null;

                string infoName = ClearString(_torrentInfo.Name);
                return result.Result.Torrents.FirstOrDefault(item => ClearString(item.Name) == infoName);
            }

            // Tìm kiếm theo hash nếu có
            return result.Result.Torrents.FirstOrDefault(item =>
                string.Equals(item.Hash, _torrentInfo.Hash, StringComparison.OrdinalIgnoreCase));
        }

        private static string ClearString(string input)
        {
            Contract.Requires(input != null);

            char[] valideChar = {
                         'A','B','C','D','E','F','G','H','I','J','K','L','M','N','O','P','Q','R','S','T','U','V','W','X','Y','Z',
                         'a','b','c','d','e','f','g','h','i','j','k','l','m','n','o','p','q','r','s','t','u','v','w','x','y','z',
                         '0','1','2','3','4','5','6','7','8','9'};
            StringBuilder result = new StringBuilder(input.Length);
            foreach (var car in input)
            {
                if (valideChar.Contains(car))
                {
                    result.Append(car);
                }
                else
                {
                    result.Append(" ");
                }
            }

            return result.ToString();
        }

        public void Dispose()
        {
            // Giải phóng tài nguyên nếu cần
            _fileBytes = null;
            GC.SuppressFinalize(this);
        }
    }
}