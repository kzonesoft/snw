#if NET40
using Kzone.Signal.Extensions;
#endif
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kzone.Signal.Client
{
    internal class Connection : BaseClientContext, IDisposable, IConnection
    {

        private string _sourceIp = null;
        private int _sourcePort = 0;
        private string _serverIp = null;
        private int _serverPort = 0;
        private bool _isTimeout = false;

        private Settings _settings = null;
        private Events _events = null;
        private NetworkStream _tcpStream = null;
        private Task _monitorIdleTask = null;
        public Connection(Events events, Settings settings, Statistics statistics, KeepaliveSettings keepaliveSettings, DebugLogger debugLogger) : base(statistics, keepaliveSettings, debugLogger)
        {
            if (string.IsNullOrEmpty(settings.Host)) throw new ArgumentNullException(nameof(settings.Host) + " {ip host is null}");
            if (settings.Port < 0) throw new ArgumentOutOfRangeException(nameof(settings.Port));
            _header = "[KzoneClient ]";
            _settings = settings;
            _events = events;
            _statistics = statistics;
            _serverPort = settings.Port;
            _maxSendBufferLength = settings.StreamBufferSize;

        }

        public string Host => _serverIp;
        public bool Connected { get; protected set; }

        public void Connect()
        {
            if (Connected) throw new InvalidOperationException("Already connected to the server.");

            if (_settings.LocalPort == 0)
            {
                _client = new TcpClient();
            }
            else
            {
                IPEndPoint ipe = new(IPAddress.Any, _settings.LocalPort);
                _client = new TcpClient(ipe);
            }
            _serverIp = IsIPAddress(_settings.Host) ? _settings.Host : GetIpFromDns(_settings.Host);

            _client.NoDelay = _settings.NoDelay;

            IAsyncResult asyncResult = null;
            WaitHandle waitHandle = null;
            bool connectSuccess = false;

            //if (!_events.IsUsingDataMode && !_events.IsUsingStreamsMode)
            //    throw new InvalidOperationException("One of either 'MessageReceived' or 'StreamReceived' events must first be set.");
            _debugLogger.Logger?.Invoke(Severity.Info, _header + "connecting to " + _serverIp + ":" + _serverPort);
            _client.LingerState = new LingerOption(true, 0);
            asyncResult = _client.BeginConnect(_serverIp, _serverPort, null, null);
            waitHandle = asyncResult.AsyncWaitHandle;


            try
            {
                connectSuccess = waitHandle.WaitOne(TimeSpan.FromSeconds(_settings.ConnectTimeoutSeconds), false);
                if (!connectSuccess)
                {
                    _client.Close();
                    _debugLogger.Logger?.Invoke(Severity.Error, _header + "timeout connecting to " + _serverIp + ":" + _serverPort);
                    throw new TimeoutException("Timeout connecting to " + _serverIp + ":" + _serverPort);
                }

                _client.EndConnect(asyncResult);

                _sourceIp = ((IPEndPoint)_client.Client.LocalEndPoint).Address.ToString();
                _sourcePort = ((IPEndPoint)_client.Client.LocalEndPoint).Port;
                _tcpStream = _client.GetStream();
                _dataStream = _tcpStream;

                if (_keepaliveSettings.EnableTcpKeepAlives) EnableKeepalives();

                Connected = true;
            }
            catch (Exception e)
            {
                _debugLogger.Logger?.Invoke(Severity.Error, _header + "exception encountered: " + Environment.NewLine + e.Message);
                _debugLogger.ExceptionRecord?.Invoke(e);
                throw;
            }


            _lastActivity = DateTime.UtcNow;
            _isTimeout = false;
#if NET40
            _dataReceiver = TaskEx.Run(()
#else
            _dataReceiver = Task.Run(()
#endif
             => DataReceiver(), base._token);
            _events.HandleServerHandShake(this, _serverIp + ":" + _serverPort);
            _debugLogger.Logger?.Invoke(Severity.Info, _header + "connected to " + _serverIp + ":" + _serverPort);
            MonitorIdleServer();
        }

        internal override async Task DataReceiver()
        {
            DisconnectReason reason = DisconnectReason.Normal;

            while (!base._token.IsCancellationRequested)
            {
                try
                {
                    if (_client == null || !_client.Connected)
                    {
                        _debugLogger.Logger?.Invoke(Severity.Debug, _header + "disconnect detected");
                        break;
                    }

                    var msg = await MessageBuilder.BuildFromStream(_dataStream, base._token).ConfigureAwait(false);
                    if (msg == null)
                    {
#if NET40
                        await TaskEx.Delay
#else
                        await Task.Delay
#endif
                        (30, base._token).ConfigureAwait(false);
                        continue;
                    }

                    _lastActivity = DateTime.UtcNow;

                    if (msg.MessageType == MessageType.Removed)
                    {
                        _debugLogger.Logger?.Invoke(Severity.Info, _header + "disconnect due to server-side removal");
                        reason = DisconnectReason.Removed;
                        break;
                    }
                    else if (msg.MessageType == MessageType.Shutdown)
                    {
                        _debugLogger.Logger?.Invoke(Severity.Info, _header + "disconnect due to server shutdown");
                        reason = DisconnectReason.Shutdown;
                        break;
                    }
                    else if (msg.MessageType == MessageType.AuthSuccess)
                    {
                        RegisterChannel();
                        _debugLogger.Logger?.Invoke(Severity.Debug, _header + "authentication successful");
                        _events?.HandleAuthenticationSucceeded(this);
                        _events?.HandleServerConnected(this, _serverIp + ":" + _serverPort);
                        _events?.HandleConnectionState(this, SignalConnectionState.Connected);
                        continue;
                    }
                    else if (msg.MessageType == MessageType.ConnectionReady)
                    {
                        RegisterChannel();
                        _events?.HandleServerConnected(this, _serverIp + ":" + _serverPort);
                        _events?.HandleConnectionState(this, SignalConnectionState.Connected);
                        continue;
                    }
                    else if (msg.MessageType == MessageType.AuthFailure)
                    {
                        _debugLogger.Logger?.Invoke(Severity.Error, _header + "authentication failed");
                        reason = DisconnectReason.AuthFailure;
                        _events?.HandleConnectionState(this, SignalConnectionState.Disconnected);
                        _events?.HandleAuthenticationFailure(this);
                        break;
                    }
                    else if (msg.MessageType == MessageType.AuthRequired)
                    {
                        _debugLogger.Logger?.Invoke(Severity.Info, _header + "authentication required by server; please authenticate using pre-shared key");
                        if (!string.IsNullOrEmpty(_settings.PresharedKey)) Authenticate(_settings.PresharedKey);
                        continue;
                    }

                    else if (msg.MessageType == MessageType.RequestPack)
                    {
                        DateTime expiration = StreamCommon.GetExpirationTimestamp(msg);
                        byte[] msgData = await StreamCommon.ReadMessageDataAsync(msg, _settings.StreamBufferSize).ConfigureAwait(false);

                        if (DateTime.UtcNow < expiration)
                        {
                            var request = new Request(
                                this,
                                msg.ConversationGuid,
                                msg.Expiration,
                                msg.Header,
                                msgData);

                            //sau khi nhận được message thì sẽ giao cho 1 task khác thực hiện, ko block quá trình đọc stream
                            HandleMessageAndReply(msg, request);
                        }
                        else
                        {
                            _debugLogger.Logger?.Invoke(Severity.Debug, _header + "expired synchronous request received and discarded");
                        }
                    }
                    else if (msg.MessageType == MessageType.ResponsePack)
                    {
                        byte[] msgData = await StreamCommon.ReadMessageDataAsync(msg, _settings.StreamBufferSize).ConfigureAwait(false);

                        if (DateTime.UtcNow < msg.Expiration)
                        {
                            HandleResponseReceived(this, msg, msgData);
                        }
                        else
                        {
                            _debugLogger.Logger?.Invoke(Severity.Debug, _header + "expired synchronous response received and discarded");
                        }
                    }
                    else if (msg.MessageType == MessageType.BroadcastPack)
                    {
                        var msgData = await StreamCommon.ReadMessageDataAsync(msg, _settings.StreamBufferSize).ConfigureAwait(false);
#if NET40
                        Task unawait = TaskEx.Run(()
#else
                        Task unawait = Task.Run(()
#endif
                        => _events.HandleDataReceived(this, msg.Header, msgData), base._token);
                    }
                    else if (msg.MessageType == MessageType.StreamPack)
                    {
                        StreamEx ws = null;

                        if (msg.ContentLength >= _settings.MaxProxiedStreamSize)
                        {
                            ws = new StreamEx(msg.ContentLength, msg.DataStream);
                            _events.HandleStreamReceived(this, msg.Header, msg.ContentLength, ws);
                        }
                        else
                        {
                            MemoryStream ms = StreamCommon.DataStreamToMemoryStream(msg.ContentLength, msg.DataStream, _settings.StreamBufferSize);
                            ws = new StreamEx(msg.ContentLength, ms);
#if NET40
                            await TaskEx.Run(()
#else
                            await Task.Run(()
#endif
                            => _events.HandleStreamReceived(this, msg.Header, msg.ContentLength, ws), base._token);
                        }
                    }
                    else
                    {
                        _debugLogger.Logger?.Invoke(Severity.Error, _header + "event handler not set for either MessageReceived or StreamReceived");
                        break;
                    }

                    _statistics.IncrementReceivedMessages();
                    _statistics.AddReceivedBytes(msg.ContentLength);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException)
                {
                    break;
                }
                catch (Exception e)
                {
                    _debugLogger?.Logger?.Invoke(Severity.Error,
                        _header + "data receiver exception for " + _serverIp + ":" + _serverPort + ":" + Environment.NewLine + e.Message + Environment.NewLine);
                    _debugLogger.ExceptionRecord?.Invoke(e);
                    break;
                }
            }

            try
            {
                Connected = false;

                if (_isTimeout) reason = DisconnectReason.Timeout;

                _debugLogger.Logger?.Invoke(Severity.Debug, _header + "data receiver terminated for " + _serverIp + ":" + _serverPort);
                _events?.HandleConnectionState(this, SignalConnectionState.Disconnected);
                _events?.HandleServerDisconnected(this, _serverIp + ":" + _serverPort, reason);
            }
            finally
            {
                await _monitorIdleTask.StopIfRunningAsync(_tokenSource).ConfigureAwait(false);
            }
        }

        private void HandleMessageAndReply(Message msg, Request request)
        {
            if (_events.OnRpcDataReceived == null) return;
#if NET40
            Task unawait = TaskEx.Run(async () =>
#else
            Task unawait = Task.Run(async () =>
#endif
            {
                var response = await _events.HandleRpcReceived(request);
                if (response != null)
                {
                    StreamCommon.ObjectToStream(response.DefaultData, out int contentLength, out Stream stream);
                    Message respMsg = new(
                        response.Header,
                        contentLength,
                        stream,
                        MessageType.ResponsePack,
                        msg.Expiration,
                        msg.ConversationGuid);
                    await SendInternalAsync(respMsg, contentLength, stream).ConfigureAwait(false);
                }
            }, base._token);

        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Authenticate(string presharedKey)
        {
            if (string.IsNullOrEmpty(presharedKey)) throw new ArgumentNullException(nameof(presharedKey));
            if (presharedKey.Length != 16) throw new ArgumentException("Preshared key length must be 16 bytes.");
            SendInternal(new()
            {
                MessageType = MessageType.AuthRequested,
                PresharedKey = Encoding.UTF8.GetBytes(presharedKey)
            }, 0, null);
        }

        private void RegisterChannel()
        {
            if (_settings.Channel < 1) return;
            SendInternal(new()
            {
                MessageType = MessageType.RegisterChannel,
                Header = HeaderEx.BuildChannel(_settings.Channel)
            }, 0, null);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _debugLogger.Logger?.Invoke(Severity.Info, _header + "disposing");
                if (Connected) Disconnect();

#if NET40
                if (_writeLock != null)
                {
                    _writeLock.Release();
                    _writeLock = null;
                }

                if (_readLock != null)
                {
                    _readLock.Release();
                    _readLock = null;
                }
#else

                _writeLock?.Dispose();
                _readLock?.Dispose();
#endif
                _settings = null;
                _events = null;
                _statistics = null;

                _sourceIp = null;
                _serverIp = null;

                _client = null;
                _dataStream = null;
                _tcpStream = null;

                _writeLock = null;
                _readLock = null;

                _dataReceiver = null;
            }
        }

        public void Disconnect(bool sendNotice = true)
        {
            if (!Connected) throw new InvalidOperationException("Not connected to the server.");

            _debugLogger.Logger?.Invoke(Severity.Info, _header + "disconnecting from " + _serverIp + ":" + _serverPort);

            if (Connected && sendNotice)
            {
                Message msg = new()
                {
                    MessageType = MessageType.Shutdown
                };
                SendInternal(msg, 0, null);
            }

            _dataReceiver.StopIfRunning(_tokenSource);

            _tcpStream?.Close();
            _tcpStream?.Dispose();

            _client?.Close();
            _client?.Dispose();

            Connected = false;

            _debugLogger.Logger?.Invoke(Severity.Info, _header + "disconnected from " + _serverIp + ":" + _serverPort);
        }

        private bool IsIPAddress(string ipAddress)
        {
            return (ipAddress?.Split('.')?.Length) == 4 && IPAddress.TryParse(ipAddress, out _);
        }

        private string GetIpFromDns(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Host is null or empty.");

            try
            {
                // Lấy danh sách địa chỉ từ DNS
                var addresses = Dns.GetHostAddresses(host);

                // Lọc ưu tiên IPv4 (hoặc tùy chọn IPv6 nếu cần)
                var ipAddress = addresses.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                             ?? throw new Exception($"No valid IP address found for host: {host}");
                return ipAddress.ToString();
            }
            catch (SocketException ex)
            {
                // Ném ngoại lệ cụ thể khi không phân giải được DNS
                throw new Exception($"DNS resolution failed for host: {host}", ex);
            }
            catch (Exception ex)
            {
                // Ném ngoại lệ nếu xảy ra lỗi khác
                throw new Exception($"Unexpected error while resolving DNS for host: {host}", ex);
            }
        }


        private void MonitorIdleServer()
        {
            _monitorIdleTask =
#if NET40
            TaskEx.Run(async () =>
#else
            Task.Run(async () =>
#endif
            {
                while (!_token.IsCancellationRequested)
                {
                    try
                    {
                        if (_settings != null && _settings.IdleServerTimeoutSeconds > 0 && ((DateTime.UtcNow - _lastActivity).TotalSeconds > _settings.IdleServerTimeoutSeconds))
                        {
                            _debugLogger.Logger?.Invoke(Severity.Info, _header + " ---last activity timeout--- !");
                            Dispose();
                        }
#if NET40
                        await TaskEx.Delay(5000, _token).ConfigureAwait(false);
#else
                        await Task.Delay(5000, _token).ConfigureAwait(false);
#endif
                    }
                    catch { }
                };
                _debugLogger.Logger?.Invoke(Severity.Info, "" + "---stopped monitor task client activity timeout--- !");
            }, _token);
        }
    }
}