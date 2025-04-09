using Kzone.Engine.Controller.Infrastructure.Api.Responses;
using Kzone.Engine.Controller.Infrastructure.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Kzone.Engine.Controller.Infrastructure.Api.Requests
{
    public abstract class BaseRequest<T> where T : BaseResponse, new()
    {
        #region Properties

        private string _baseUrl;

        protected Uri BaseUrl
        {
            get
            {
                if (_baseUrl == null)
                    return null;
                return new Uri(_baseUrl);
            }
            set
            {
                _baseUrl = value?.ToString();
            }
        }

        protected string Token { get; set; }

        protected UrlAction UrlAction { get; set; } = UrlAction.Default;

        protected IList<string> TorrentHash { get; } = new List<string>();

        protected Dictionary<string, string> Settings { get; } = new Dictionary<string, string>();

        #region Input

        #endregion

        #region Output

        protected bool UseCacheId { get; set; } = true;

        protected int CacheId { get; set; }

        protected bool HasTorrentList { get; set; }

        #endregion

        #endregion

        #region Fluent Setter

        public BaseRequest<T> SetBaseUrl(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            BaseUrl = uri;
            return this;
        }

        public BaseRequest<T> SetAction(UrlAction urlAction)
        {
            if (urlAction == null)
                throw new ArgumentNullException(nameof(urlAction));

            if (!CheckAction(urlAction))
                throw new InvalidOperationException(nameof(urlAction) + " invalide for this request");

            UrlAction = urlAction;
            return this;
        }

        public BaseRequest<T> SetTorrentHash(string hash)
        {
            Contract.Requires(hash != null);

            if (string.IsNullOrWhiteSpace(hash))
                throw new ArgumentNullException(nameof(hash));

            hash = hash.Trim().ToUpperInvariant();
            if (!TorrentHash.Contains(hash))
                TorrentHash.Add(hash);
            return this;
        }

        public BaseRequest<T> SetTorrentHash(IEnumerable<string> hashs)
        {
            if (hashs == null)
                throw new ArgumentNullException(nameof(hashs));

            foreach (string hash in hashs)
            {
                if (string.IsNullOrWhiteSpace(hash))
                    throw new FormatException("Invalide hash format");

                var temphash = hash.Trim();
                if (!TorrentHash.Any(h => h.Equals(temphash, StringComparison.OrdinalIgnoreCase)))
                    TorrentHash.Add(temphash);
            }

            return this;
        }

        public BaseRequest<T> SetSetting(string key, string value)
        {
            Contract.Requires(key != null);
            Contract.Requires(value != null);

            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Invalid key", nameof(key));

            key = key.Trim();
            Settings[key] = value;
            return this;
        }

        public BaseRequest<T> SetSetting(string key, bool value)
        {
            return SetSetting(key, value ? "true" : "false");
        }

        public BaseRequest<T> SetSetting(string key, int value)
        {
            return SetSetting(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public BaseRequest<T> IncludeTorrentList(bool value)
        {
            HasTorrentList = value;
            return this;
        }

        public BaseRequest<T> SetCacheId(int cacheId)
        {
            CacheId = cacheId;
            return this;
        }

        public BaseRequest<T> UnableCache()
        {
            UseCacheId = true;
            return this;
        }

        public BaseRequest<T> DisableCache()
        {
            UseCacheId = false;
            return this;
        }

        #endregion

        protected abstract bool CheckAction(UrlAction action);

        protected abstract void ToUrl(StringBuilder sb);

        public Uri ToUrl()
        {
            return ToUrl(Token);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "Lower case required")]
        public Uri ToUrl(string token)
        {
            if (token == null)
                throw new InvalidOperationException("Token can't be empty.");

            if (_baseUrl == null)
                throw new InvalidOperationException("BaseUrl not set.");

            StringBuilder sb = new StringBuilder();

            sb.Append(_baseUrl);
            sb.Append("?token=").Append(token);

            if (UrlAction != UrlAction.Default)
                sb.Append("&action=").Append(UrlAction.ActionValue.ToLowerInvariant());

            foreach (string torrentHash in TorrentHash)
                sb.Append("&hash=").Append(torrentHash);

            foreach (var setting in Settings)
            {
                sb.Append("&s=").Append(Uri.EscapeUriString(setting.Key));
                sb.Append("&v=").Append(setting.Value);
            }

            if (HasTorrentList)
                sb.Append("&list=1");

            if (UseCacheId)
                sb.Append("&cid=").Append(CacheId);

            ToUrl(sb);

            return new Uri(sb.ToString());
        }

        // Phương thức trừu tượng mà các lớp con phải triển khai
        protected abstract void OnProcessingRequest(HttpClient httpClient, HttpRequestMessage requestMessage);
        protected abstract void OnProcessedRequest(T result);

        // Phương thức xử lý request thực hiện bằng HttpClient
        public T ProcessRequest(string token, string logOn, string password, Cookie cookie)
        {
            try
            {
                Uri uri = ToUrl(token);

                using (var httpClientHandler = new HttpClientHandler())
                {
                    // Thiết lập thông tin xác thực
                    httpClientHandler.Credentials = new NetworkCredential(logOn, password);

                    // Thiết lập cookie nếu có
                    if (cookie != null)
                    {
                        httpClientHandler.CookieContainer = new CookieContainer();
                        var cookieUri = cookie.Domain != null ? new Uri(cookie.Domain) : BaseUrl;
                        httpClientHandler.CookieContainer.Add(uri, new Cookie(cookie.Name, cookie.Value));
                    }

                    // Tạo HttpClient
                    using (var httpClient = new HttpClient(httpClientHandler))
                    {
                        httpClient.Timeout = TimeSpan.FromSeconds(30);

                        // Tạo HttpRequestMessage
                        using (var requestMessage = new HttpRequestMessage(HttpMethod.Get, uri))
                        {
                            // Cho phép lớp con xử lý request
                            OnProcessingRequest(httpClient, requestMessage);

                            // Gửi request và đọc response
                            var response = httpClient.SendAsync(requestMessage).Result;

                            // Đọc và xử lý dữ liệu phản hồi
                            var jsonResult = response.Content.ReadAsStringAsync().Result;

                            var result = JsonParser.ParseJsonResult(jsonResult);

                            if (result != null && result.CacheId != 0)
                                CacheId = result.CacheId;

                            result.StatusCode = response.StatusCode;

                            var ret = new T { Result = result };
                            OnProcessedRequest(ret);
                            return ret;
                        }
                    }
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
            catch (TaskCanceledException) // Timeout
            {
                return default;
            }
        }
    }
}