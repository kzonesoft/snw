using Kzone.Engine.Controller.Domain.Entities;
using Kzone.Engine.Controller.Infrastructure.Api.Responses;
using Kzone.Engine.Controller.Infrastructure.Helpers;
using System;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
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

        protected override void OnProcessingRequest(System.Net.HttpWebRequest wr)
        {
            if (wr == null)
            {
                throw new ArgumentNullException(nameof(wr));
            }

            if (InputStream != null)
            {
                string boundary = "---------------------------" + DateTime.Now.Ticks.ToString("x", CultureInfo.InvariantCulture);
                byte[] boundarybytes = Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

                wr.ContentType = "multipart/form-data; boundary=" + boundary;
                wr.Method = "POST";

                using (var ms = new ChunkedMemoryStream())
                {
                    ms.Write(boundarybytes, 0, boundarybytes.Length);
                    const string headerTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n";
                    string header = string.Format(CultureInfo.InvariantCulture, headerTemplate, "torrent_file", "file.torrent", "application/octet-stream");
                    byte[] headerbytes = Encoding.UTF8.GetBytes(header);
                    ms.Write(headerbytes, 0, headerbytes.Length);

                    byte[] contenttypebytes = Encoding.ASCII.GetBytes("Content-Type: application/x-bittorrent\r\n\r\n");
                    ms.Write(contenttypebytes, 0, contenttypebytes.Length);

                    byte[] buffer = new byte[4096];
                    int bytesRead;
                    while ((bytesRead = InputStream.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        ms.Write(buffer, 0, bytesRead);
                    }
                    //request.InputStream.Close();

                    byte[] trailer = Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
                    ms.Write(trailer, 0, trailer.Length);

#if !PORTABLE
                    wr.ContentLength = ms.Length;
#else
                    wr.Headers["Content-Length"] = ms.Length.ToString(CultureInfo.InvariantCulture);
#endif

                    // Debug
                    //ms.Position = 0;
                    //var srMs = new System.IO.StreamReader(ms);
                    //string post = srMs.ReadToEnd();



#if !PORTABLE
                    System.IO.Stream rs = wr.GetRequestStream();
#else
                    System.IO.Stream rs = wr.GetRequestStreamAsync().GetAwaiter().GetResult();
#endif
                    if (rs != null)
                    {
                        using (rs)
                        {
                            ms.Position = 0;
                            while ((bytesRead = ms.Read(buffer, 0, buffer.Length)) != 0)
                            {
                                rs.Write(buffer, 0, bytesRead);
                            }

                            rs.Flush();
                        }
                    }
                }
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
