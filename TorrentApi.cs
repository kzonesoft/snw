using Kzone.Engine.Controller.Domain.Entities;
using Kzone.Engine.Controller.Domain.Exceptions;
using Kzone.Engine.Controller.Domain.Interfaces;
using Kzone.Engine.Controller.Infrastructure.Api.Requests;
using Kzone.Engine.Controller.Infrastructure.Api.Responses;
using Kzone.Engine.Controller.Infrastructure.Helpers;
using Kzone.Semaphore;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kzone.Engine.Controller.Infrastructure.Api
{
    public class TorrentApi : ITorrentApi, IDisposable
    {
        private readonly AsyncSemaphore _semaphore = new(1);
        private string _logOn;
        private string _password;
        private string _ip = IPAddress.Loopback.ToString();
        private int _port;
        private string _baseUrl;
        private Cookie _cookie;
        private string _token;
        private int _cacheId;

        private DateTime _tokenGetTime = DateTime.MinValue;
        private TimeSpan _timeSpanTokenTimeout = TimeSpan.FromMinutes(20);
        private Uri _tokenUrl => new(_baseUrl + "token.html");
        private bool _useCache;

        private int _speedZeroCounter = 0;

        private const int ENGINE_DATA_TIMEOUT_MINUTES = 15;
        private const int MAX_ZERO_SPEED_COUNT = 1200;

        private DateTime _lastGetDataSuccess = DateTime.MinValue;

        private readonly object _lock = new object();

        // HttpClient instance, tạo khi cần
        private HttpClient _httpClient;
        private bool _disposed;

        public TorrentApi(bool useCache = false)
        {
            _useCache = useCache;
        }

        private string Token
        {
            get
            {
                if (_token == null || DateTime.Now - _tokenGetTime > _timeSpanTokenTimeout)
                {
                    GetToken();
                }
                return _token;
            }
        }

        // Tạo và quản lý HttpClient instance
        private HttpClient HttpClient
        {
            get
            {
                if (_httpClient == null)
                {
                    _httpClient = CreateHttpClient();
                }
                return _httpClient;
            }
        }

        // Tạo mới HttpClient với cấu hình hiện tại
        private HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                Credentials = new NetworkCredential(_logOn, _password)
            };

            // Nếu có cookie, thêm vào HttpClient
            if (_cookie != null)
            {
                handler.CookieContainer = new CookieContainer();
                handler.CookieContainer.Add(new Uri(_baseUrl), _cookie);
            }

            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            return client;
        }

        public void ConfigureAccess(int port, string userName, string password)
        {
            _semaphore.Wait();
            try
            {
                if (port <= 0 || port >= 65536)
                    throw new ArgumentOutOfRangeException(nameof(port));

                _logOn = userName;
                _password = password;
                _port = port;
                _speedZeroCounter = 0;
                _lastGetDataSuccess = DateTime.MinValue;
                _tokenGetTime = DateTime.MinValue;
                _baseUrl = string.Format(System.Globalization.CultureInfo.InvariantCulture, "http://{0}:{1}/api/", _ip, _port);

                // Tạo HttpClient mới với cấu hình đã cập nhật
                DisposeHttpClient();

#if DEBUG
                Console.WriteLine($"ENGINE API SESSION GENERATE : {_baseUrl}{Environment.NewLine}" +
                                  $"                              {_logOn}" +
                                  $"                              {Environment.NewLine}" +
                                  $"                              {_password}");
#endif
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<bool> IsApiAlive(CancellationToken token)
        {
            try
            {
                // Sử dụng HttpClient được tạo mới cho thao tác này để tránh ảnh hưởng bởi cấu hình hiện tại
                using (var httpClient = new HttpClient())
                {
                    var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_logOn}:{_password}"));
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                    httpClient.Timeout = TimeSpan.FromSeconds(2);

                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        cts.CancelAfter(TimeSpan.FromSeconds(2));

                        var response = await httpClient.GetAsync(_baseUrl, cts.Token).ConfigureAwait(false);
                        return response.IsSuccessStatusCode;
                    }
                }
            }
            catch (TaskCanceledException)
            {
                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException("The request was canceled.", token);
                }
                return false; // Timeout
            }
            catch (Exception)
            {
                return false; // Network or other errors
            }
        }

        public async Task<IEnumerable<Torrent>> GetTorrents(bool frozenCheck = false)
        {
            IEnumerable<Torrent> torrents = null;
            try
            {
                var request = new Request();
                request.IncludeTorrentList(true);
                if (_useCache)
                {
                    SetCacheId(request, _cacheId);
                }
                var response = await ExecuteRequest(request).ConfigureAwait(false);
                torrents = response == null || response.Result == null
                    ? null
                    : response.Result.Torrents;
            }
            catch
            {
                torrents = null;
            }
            if (frozenCheck && EngineFrozenCheck(torrents))
            {
                throw new EngineFrozenException("Detected the engine frozen !!!");
            }
            return torrents;
        }

        #region Command

        public async Task<Response> StartTorrent(string hash)
        {
            Contract.Requires(hash != null);
            return await ActionTorrentAsync(UrlAction.Start, hash);
        }

        public async Task<Response> StartTorrents(IEnumerable<string> hashs)
        {
            return await ActionTorrentAsync(UrlAction.Start, hashs);
        }

        public async Task<Response> StopTorrent(string hash)
        {
            Contract.Requires(hash != null);
            return await ActionTorrentAsync(UrlAction.Stop, hash);
        }

        public async Task<Response> StopTorrents(IEnumerable<string> hashs)
        {
            return await ActionTorrentAsync(UrlAction.Stop, hashs);
        }

        public async Task<Response> RemoveTorrent(string hash)
        {
            Contract.Requires(hash != null);
            return await ActionTorrentAsync(UrlAction.Remove, hash);
        }

        public async Task<Response> RemoveTorrents(IEnumerable<string> hashs)
        {
            Contract.Requires(hashs != null);
            return await ActionTorrentAsync(UrlAction.Remove, hashs);
        }

        private async Task<Response> ActionTorrentAsync(UrlAction urlAction, string hash)
        {
            Contract.Requires(hash != null);

            var request = new Request()
                .SetAction(urlAction)
                .IncludeTorrentList(true)
                .SetTorrentHash(hash);

            return await ExecuteRequest(request);
        }

        private async Task<Response> ActionTorrentAsync(UrlAction urlAction, IEnumerable<string> hashs)
        {
            var request = new Request()
                .SetAction(urlAction)
                .IncludeTorrentList(true)
                .SetTorrentHash(hashs);

            return await ExecuteRequest(request);
        }

        #endregion

        #region New Torrent

        //Thêm mới torrent
        public async Task<bool> AddTorrent(string savePath, byte[] torrentBytes)
        {
            if (string.IsNullOrEmpty(savePath)) return false;

            var torrentFileSavePath = Path.Combine(savePath, "_kz");

            // Set download and torrent file paths
            bool isPathSet = await SetPathsAsync(savePath, torrentFileSavePath).ConfigureAwait(false);
            if (!isPathSet) return false;

            await TaskEx.Delay(1000).ConfigureAwait(false);

            // Verify settings with the engine
            bool isVerified = await VerifyPathsAsync(savePath, torrentFileSavePath).ConfigureAwait(false);
            if (!isVerified) return false;

            using (var stream = new MemoryStream(torrentBytes))
            {
                var postTorrentResult = await PostTorrent(stream).ConfigureAwait(false);
                return postTorrentResult != null;
            }
        }

        //Set đường dẫn
        private async Task<bool> SetPathsAsync(string downloadPath, string torrentFilePath)
        {
            try
            {
                // Kiểm tra đầu vào
                if (string.IsNullOrEmpty(downloadPath) || string.IsNullOrEmpty(torrentFilePath))
                {
                    return false;
                }

                var settings = new Dictionary<string, object>()
                {
                    { "dir_active_download", downloadPath },
                    { "dir_torrent_files", torrentFilePath }
                };

                var setPathResult = await SetSettings(settings).ConfigureAwait(false);

                // Kiểm tra kết quả chi tiết hơn
                if (setPathResult == null || setPathResult.Result == null)
                {
                    return false;
                }

                return setPathResult.Result.StatusCode == HttpStatusCode.OK;
            }
            catch (Exception)
            {
                return false;
            }
        }

        //kiểm tra lại kết quả
        private async Task<bool> VerifyPathsAsync(string expectedDownloadPath, string expectedTorrentFilePath)
        {
            try
            {
                var settingsResult = await GetSettings().ConfigureAwait(false);
                if (settingsResult?.Result?.Settings == null) return false;

                var settings = settingsResult.Result.Settings;

                var downloadPathSetting = settings.FirstOrDefault(x => x.Key == "dir_active_download");
                var torrentPathSetting = settings.FirstOrDefault(x => x.Key == "dir_torrent_files");

                if (downloadPathSetting == null || torrentPathSetting == null) return false;

                var downloadPathInServer = downloadPathSetting.Value?.ToString();
                var torrentPathInServer = torrentPathSetting.Value?.ToString();

                if (string.IsNullOrEmpty(downloadPathInServer) || string.IsNullOrEmpty(torrentPathInServer))
                    return false;

                // Chuẩn hóa các đường dẫn để so sánh
                var normalizedExpectedDownloadPath = Path.GetFullPath(expectedDownloadPath).TrimEnd('\\', '/');
                var normalizedExpectedTorrentPath = Path.GetFullPath(expectedTorrentFilePath).TrimEnd('\\', '/');
                var normalizedDownloadPathInServer = Path.GetFullPath(downloadPathInServer).TrimEnd('\\', '/');
                var normalizedTorrentPathInServer = Path.GetFullPath(torrentPathInServer).TrimEnd('\\', '/');

                // So sánh sau khi chuẩn hóa
                return string.Equals(normalizedDownloadPathInServer, normalizedExpectedDownloadPath, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(normalizedTorrentPathInServer, normalizedExpectedTorrentPath, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<AddStreamResponse> PostTorrent(Stream inputStream)
        {
            Contract.Requires(inputStream != null);

            GetToken(); // Đảm bảo token còn hiệu lực
            AddStreamResponse result;
            using (var request = new AddStreamRequest())
            {
                request.SetFile(inputStream);
                request.SetAction(UrlAction.AddFile);
                request.IncludeTorrentList(true);

                if (_useCache)
                {
                    SetCacheId(request, 0);
                }
                result = await ExecuteRequest(request);
            }
            return result;
        }

        #endregion

        #region Settings

        public async Task<Response> GetSettings()
        {
            Request request = new Request();
            request.SetAction(UrlAction.GetSettings);
            return await ExecuteRequest(request);
        }

        public async Task<Response> SetSetting(string key, object value)
        {
            Request request = new Request();
            request.SetAction(UrlAction.SetSetting);

            // Determine the type of the value and set it accordingly
            switch (value)
            {
                case string strValue:
                    request.SetSetting(key, strValue);
                    break;
                case bool boolValue:
                    request.SetSetting(key, boolValue);
                    break;
                case int intValue:
                    request.SetSetting(key, intValue);
                    break;
                default:
                    throw new ArgumentException("Unsupported setting type");
            }

            return await ExecuteRequest(request);
        }

        public async Task<Response> SetSettings(Dictionary<string, object> settings)
        {
            Request request = new Request();
            request.SetAction(UrlAction.SetSetting);

            // Loop through settings and handle different data types dynamically
            foreach (var setting in settings)
            {
                switch (setting.Value)
                {
                    case string strValue:
                        request.SetSetting(setting.Key, strValue);
                        break;
                    case bool boolValue:
                        request.SetSetting(setting.Key, boolValue);
                        break;
                    case int intValue:
                        request.SetSetting(setting.Key, intValue);
                        break;
                    default:
                        throw new ArgumentException($"Unsupported setting type for key: {setting.Key}");
                }
            }

            return await ExecuteRequest(request);
        }

        #endregion

        // Chuyển đổi GetToken từ WebRequest sang HttpClient, sử dụng HttpClient property
        private void GetToken()
        {
            try
            {
                // Tạo HttpClient tạm thời cho việc lấy token
                using (var httpClient = new HttpClient())
                {
                    // Thiết lập thông tin xác thực
                    var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_logOn}:{_password}"));
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                    httpClient.Timeout = TimeSpan.FromSeconds(10);

                    // Gửi request đồng bộ (vì phương thức gốc là đồng bộ)
                    var response = httpClient.GetAsync(_tokenUrl).Result;

                    // Xử lý response
                    if (response.IsSuccessStatusCode)
                    {
                        string result = response.Content.ReadAsStringAsync().Result;

                        if (string.IsNullOrEmpty(result))
                        {
                            throw new ServerUnavailableException("Unable to retrieve WebUI token");
                        }

                        // Xử lý cookies
                        if (response.Headers.Contains("Set-Cookie"))
                        {
                            var cookies = response.Headers.GetValues("Set-Cookie");
                            var cookieString = string.Join(";", cookies);

                            if (cookieString.Contains("GUID"))
                            {
                                var tab1 = cookieString.Split(';');
                                if (tab1.Length >= 1)
                                {
                                    var cookiestab = tab1[0].Split('=');
                                    if (cookiestab.Length >= 2)
                                    {
                                        _cookie = new Cookie(cookiestab[0], cookiestab[1]) { Domain = _baseUrl };

                                        // Tạo lại HttpClient vì cookie đã thay đổi
                                        DisposeHttpClient();
                                    }
                                }
                            }
                        }

                        // Xử lý token từ HTML response
                        int indexStart = result.IndexOf('<');
                        int indexEnd = result.IndexOf('>');
                        while (indexStart >= 0 && indexEnd >= 0 && indexStart <= indexEnd)
                        {
                            result = result.Remove(indexStart, indexEnd - indexStart + 1);

                            indexStart = result.IndexOf('<');
                            indexEnd = result.IndexOf('>');
                        }

                        _tokenGetTime = DateTime.Now;
                        _token = result;
                    }
                    else if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        throw new InvalidCredentialException();
                    }
                    else
                    {
                        throw new ServerUnavailableException($"Unable to retrieve WebUI token. Status: {response.StatusCode}");
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                _tokenGetTime = DateTime.MinValue;
                throw new ServerUnavailableException("Unable to retrieve WebUI token", ex);
            }
            catch (Exception ex) when (!(ex is ServerUnavailableException || ex is InvalidCredentialException))
            {
                _tokenGetTime = DateTime.MinValue;
                throw new ServerUnavailableException("Unable to retrieve WebUI token", ex);
            }
        }

        private async Task<TResponse> ExecuteRequest<TResponse>(BaseRequest<TResponse> request) where TResponse : BaseResponse, new()
        {
            await _semaphore.WaitAsync();
            try
            {
                if (request == null)
                    throw new ArgumentNullException(nameof(request));

                string token = Token;
                request.SetBaseUrl(new Uri(_baseUrl));

                // Vẫn sử dụng phương thức cũ vì cần điều chỉnh nhiều lớp con khác
                var response = request.ProcessRequest(token, _logOn, _password, _cookie);

                if (response?.Result != null && response?.Result.CacheId != 0)
                {
                    _cacheId = response.Result.CacheId;
                }
                return response;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private void SetCacheId<T>(BaseRequest<T> request, int cacheId) where T : BaseResponse, new()
        {
            Contract.Requires(request != null);

            if (cacheId != 0)
            {
                request.UnableCache();
                request.SetCacheId(cacheId);
            }
        }

        //kiểm tra nếu engine vẫn đang chạy mà không lấy được dữ liệu, hoặc bị treo connection
        private bool EngineFrozenCheck(IEnumerable<Torrent> torrents)
        {
            lock (_lock)
            {
                if (torrents != null)
                {
                    _lastGetDataSuccess = DateTime.Now;

                    var downloadList = torrents.Where(x => x.Status == "Downloading").ToArray();
#if DEBUG
                    Console.WriteLine($"{DateTime.Now} [ENGINE REPORT] Download counter : [{downloadList.Count()}]");
#endif
                    if (downloadList.Any())
                    {
                        var downloadSpeed = downloadList.Sum(y => y.DownloadSpeed);
                        if (downloadSpeed == 0)
                        {
                            _speedZeroCounter++;
#if DEBUG
                            Console.WriteLine($"{DateTime.Now} [FREEZER ENGINE DETECTOR] Detect download zero count [{_speedZeroCounter}]");
#endif
                        }
                        else
                        {
#if DEBUG
                            Console.WriteLine($"{DateTime.Now} [ENGINE REPORT] Current speed : [{downloadSpeed}]");
#endif
                            ResetZeroSpeedCounter();
                        }
                    }
                    else
                    {
                        ResetZeroSpeedCounter();
                    }
                }

                if (_lastGetDataSuccess != DateTime.MinValue && DateTime.Now - _lastGetDataSuccess > TimeSpan.FromMinutes(ENGINE_DATA_TIMEOUT_MINUTES))
                {
                    return true;
                }

                if (_speedZeroCounter > MAX_ZERO_SPEED_COUNT) //3s thực hiện 1 lần, 1 phút 20 lần,  30 phút 600 lần, 60 phút 1200 lần
                {
                    return true;
                }

                return false;
            }
        }

        private void ResetZeroSpeedCounter()
        {
            if (_speedZeroCounter != 0)
                _speedZeroCounter = 0;
        }

        // Giải phóng HttpClient
        private void DisposeHttpClient()
        {
            if (_httpClient != null)
            {
                _httpClient.Dispose();
                _httpClient = null;
            }
        }

        // Triển khai IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                DisposeHttpClient();
            }

            _disposed = true;
        }

        ~TorrentApi()
        {
            Dispose(false);
        }
    }
}