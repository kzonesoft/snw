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
    public class AddStreamRequest : BaseAddRequest<AddStreamResponse>, IDisposable
    {
        #region Properties

        private System.IO.Stream _inputStream;
        public System.IO.Stream InputStream
        {
            get { return _inputStream; }
            protected set
            {
                _inputStream?.Dispose();
                _inputStream = value;
            }
        }

        private TorrentInfo _torrentInfo;
        public TorrentInfo TorrentInfo => _torrentInfo;

        #endregion

        #region Fluent Setter

        public AddStreamRequest SetFile(System.IO.Stream inputStream)
        {
            Contract.Requires(inputStream != null);
            Contract.Requires(inputStream.CanRead);

            if (inputStream == null)
                throw new ArgumentNullException(nameof(inputStream));
            if (!inputStream.CanRead)
                throw new ArgumentException("Argument inputStream must be readable");

            _inputStream = new ChunkedMemoryStream();
            inputStream.CopyTo(_inputStream);

            _inputStream.Position = 0;
            var result = BencodeDecoder.Decode(_inputStream);
            _inputStream.Position = 0;

            if (result == null || result.Length == 0 || !(result[0] is BDictionary))
            {
                throw new InvalidOperationException("Invalid torrent stream");
            }

            var torrent = TorrentInfo.Parse((BDictionary)result[0]);

            _torrentInfo = torrent;

            return this;
        }

        #endregion

        protected override void ToUrl(StringBuilder sb)
        {
            base.ToUrl(sb);

            if (UrlAction == UrlAction.AddFile && _inputStream == null)
                throw new InvalidOperationException("FileStream is missing with AddFile action");
        }

        // Triển khai đúng phương thức từ lớp cha
        protected override void OnProcessingRequest(HttpClient httpClient, HttpRequestMessage requestMessage)
        {
            if (InputStream != null)
            {
                string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x", CultureInfo.InvariantCulture);

                // Chuyển từ GET sang POST
                requestMessage.Method = HttpMethod.Post;

                // Tạo nội dung multipart
                var multipartContent = new MultipartFormDataContent(boundary);

                // Chuẩn bị dữ liệu torrent
                var memoryStream = new MemoryStream();
                InputStream.Position = 0;
                InputStream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                // Tạo content từ memory stream
                var streamContent = new StreamContent(memoryStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                // Thêm file torrent vào form data
                multipartContent.Add(streamContent, "torrent_file", "file.torrent");

                // Gán nội dung cho request
                requestMessage.Content = multipartContent;
            }
        }

        protected override void OnProcessedRequest(AddStreamResponse result)
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

        protected override Torrent FindAddedTorrent(AddStreamResponse result)
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

        ~AddStreamRequest()
        {
            // Finalizer calls Dispose(false)
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || _inputStream == null) return;
            // free managed resources
            _inputStream.Dispose();
            _inputStream = null;
        }
    }
}