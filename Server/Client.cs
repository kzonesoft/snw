#if NET40
using Kzone.Signal.Extensions;


#endif


using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Kzone.Signal.Server
{
    internal class Client : BaseClientContext, IDisposable, IClient
    {
        private NetworkStream _networkStream = null;
        private Guid _clientId;
        private bool _allowRequest;
        private string _ipPort;
        private string _ipOnly;
        private string _identityId;
        private string _role;
        private string _group;
        private int _channel;
        private string _appVersion;
        private bool _isPreshareKeyAuth = false;
        private DateTime _lastAuth;
        private Events _events;
        private Settings _settings;
        private bool _disposed = false;
        public Guid Id => _clientId;
        public string IpPort => _ipPort;
        public string IpOnly => _ipOnly;
        public string IdentityId
        {
            get => _identityId;
            set => _identityId = value;
        }
        public string Role
        {
            get => _role;
            set => _role = value;
        }
        public int Channel
        {
            get => _channel;
            set => _channel = value;
        }
        public string Group
        {
            get { return _group; }
            set { _group = value; }
        }
        public bool AllowRequest
        {
            get => _allowRequest;
            set => _allowRequest = value;
        }

        public string AppVersion
        {
            get { return _appVersion; }
            set { _appVersion = value; }
        }


        public DateTime LastAuth
        {
            get => _lastAuth;
            set => _lastAuth = value;
        }

        public Client(TcpClient tcpClient, Events events, Settings settings, Statistics statistics, KeepaliveSettings keepaliveSettings, DebugLogger debugLogger) : base(statistics, keepaliveSettings, debugLogger)
        {
            if (tcpClient == null) throw new ArgumentNullException(nameof(tcpClient));
            _clientId = Guid.NewGuid();
            _ipPort = tcpClient.Client.RemoteEndPoint.ToString();
            _ipOnly = tcpClient.Client.RemoteEndPoint.ToString().Split(':').FirstOrDefault();
            _maxSendBufferLength = settings.StreamBufferSize;
            _client = tcpClient;
            _events = events;
            _settings = settings;
            _statistics = statistics;
            _networkStream = tcpClient?.GetStream();
            _dataStream = _networkStream;
            _lastActivity = DateTime.UtcNow;
            if (settings.Keepalive.EnableTcpKeepAlives) EnableKeepalives();
        }

        public double CalculatorLastActivity()
        {
            return (DateTime.UtcNow - _lastActivity).TotalSeconds;
        }

        internal void BeginReceiver()
        {
            if (_dataReceiver.IsRunning())
                throw new Exception(nameof(_dataReceiver) + " running !");

            _debugLogger.Logger?.Invoke(Severity.Debug, _header + "starting data receiver for " + IpPort);
#if NET40
            _dataReceiver = TaskEx.Run(() => DataReceiver(), _token);
#else
            _dataReceiver = Task.Run(() => DataReceiver(), _token);
#endif

            if (!string.IsNullOrEmpty(_settings.PresharedKey))
            {
                _debugLogger.Logger?.Invoke(Severity.Debug, _header + "requesting authentication material from " + IpPort);
                SendInternal(new()
                {
                    MessageType = MessageType.AuthRequired
                }, 0, null);
            }
            else //nếu không set pre-key thì gửi thông báo sẵn sàng
            {
                _debugLogger.Logger?.Invoke(Severity.Debug, _header + "requesting connection ready " + IpPort);

                SendInternal(new Message
                {
                    MessageType = MessageType.ConnectionReady
                }, 0, null);
            }
        }


        public void Disconnect(MessageType status = MessageType.Disconnect, bool sendNotice = false)
        {
            if (sendNotice)
            {
                SendInternal(new Message() { MessageType = status }, 0, null);
            }
            Dispose();
        }

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
                // Hủy các task đang chạy, hủy cancellation token
                _dataReceiver?.StopIfRunning(_tokenSource);
            }

            _disposed = true;
        }


        #region DATARECEIVED
        internal override async Task DataReceiver()
        {
            while (!_token.IsCancellationRequested)
            {
                try
                {
                    if (!IsConnected()) break;

                    var msg = await MessageBuilder.BuildFromStream(_dataStream, _token).ConfigureAwait(false);
                    if (msg == null)
                    {
#if NET40
                        await TaskEx.Delay(30, _token).ConfigureAwait(false);
#else
                        await Task.Delay(30, _token).ConfigureAwait(false);
#endif
                        continue;
                    }

                    if (!string.IsNullOrEmpty(_settings.PresharedKey) && !_isPreshareKeyAuth)
                    {
                        _debugLogger.Logger?.Invoke(Severity.Debug, _header + "message received from unauthenticated endpoint " + IpPort);

                        byte[] data = null;
                        Message authMsg = null;
                        int contentLength = 0;
                        Stream authStream = null;

                        if (msg.MessageType == MessageType.AuthRequested)
                        {
                            // check preshared key
                            if (msg.PresharedKey != null && msg.PresharedKey.Length > 0)
                            {
                                var clientPsk = Encoding.UTF8.GetString(msg.PresharedKey).Trim();
                                if (_settings.PresharedKey.Trim().Equals(clientPsk))
                                {
                                    _debugLogger.Logger?.Invoke(Severity.Debug, _header + "accepted authentication for " + IpPort);
                                    _isPreshareKeyAuth = true;
                                    _events.HandleAuthenticationSucceeded(this, IpPort);
                                    data = Encoding.UTF8.GetBytes("Authentication successful");
                                    StreamCommon.BytesToStream(data, 0, out contentLength, out authStream);
                                    authMsg = new Message(null, contentLength, authStream, MessageType.AuthSuccess, default, null);
                                    SendInternal(authMsg, 0, null);
                                    continue;
                                }
                                else
                                {
                                    _debugLogger.Logger?.Invoke(Severity.Warn, _header + "declined authentication for " + IpPort);
                                    _events.HandleAuthenticationFailed(this, IpPort);
                                    Disconnect(MessageType.AuthFailure, true);
                                    break;
                                }
                            }
                        }
                        _debugLogger.Logger?.Invoke(Severity.Warn, _header + "no authentication material for " + IpPort);
                        _events.HandleAuthenticationFailed(this, IpPort);
                        Disconnect(MessageType.AuthFailure, true);
                        break;
                    }
                    if (msg.MessageType == MessageType.RegisterChannel)
                    {
                        _channel = msg.Header.GetChannel();
                    }
                    else if (msg.MessageType == MessageType.Shutdown)
                    {
                        _debugLogger.Logger?.Invoke(Severity.Debug, _header + "client " + IpPort + " is disconnecting");
                        break;
                    }
                    else if (msg.MessageType == MessageType.Removed)
                    {
                        _debugLogger.Logger?.Invoke(Severity.Debug, _header + "sent disconnect notice to " + IpPort);
                        break;
                    }

                    else if (msg.MessageType == MessageType.RequestPack)
                    {
                        DateTime expiration = StreamCommon.GetExpirationTimestamp(msg);
                        byte[] msgData = await StreamCommon.ReadMessageDataAsync(msg, _settings.StreamBufferSize).ConfigureAwait(false);

                        if (DateTime.UtcNow < expiration)
                        {
                            Request request = new(this, msg.ConversationGuid, msg.Expiration, msg.Header, msgData);

                            await HandleMessageAndReply(msg, request).ConfigureAwait(false);
                        }
                        else
                        {
                            _debugLogger.Logger?.Invoke(Severity.Debug, _header + "expired synchronous request received and discarded from " + IpPort);
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
                            _debugLogger.Logger?.Invoke(Severity.Debug, _header + "expired synchronous response received and discarded from " + IpPort);
                        }
                    }

                    else if (msg.MessageType == MessageType.BroadcastPack)
                    {
                        var msgData = await StreamCommon.ReadMessageDataAsync(msg, _settings.StreamBufferSize).ConfigureAwait(false);
#if NET40
                        Task unawait = TaskEx.Run(() =>
#else
                        Task unawait = Task.Run(() =>
#endif
                        _events.HandleDataReceived(this, msg.Header, msgData), _token);

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
                            await TaskEx.Run(() =>
#else
                            await Task.Run(() =>
#endif
                            _events.HandleStreamReceived(this, msg.Header, msg.ContentLength, ws), _token);
                        }
                    }
                    else
                    {
                        _debugLogger.Logger?.Invoke(Severity.Error, _header + "event handler not set for either MessageReceived or StreamReceived");
                        break;
                    }

                    _lastActivity = DateTime.UtcNow;
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
                        _header + "data receiver exception for " + IpPort + ":" +
                        Environment.NewLine +
                        e.Message +
                        Environment.NewLine);
                    break;
                }
            }
            try
            {
                _events?.HandleClientDisconnected(this, IpPort, DisconnectReason.Shutdown);
                _debugLogger?.Logger?.Invoke(Severity.Debug, _header + "client " + IpPort + " disconnected");
            }
            finally
            {
                _networkStream?.Close();
                _networkStream?.Dispose();

                _client?.Close();
                _client?.Dispose();

                _events = null;
                _settings = null;
                _statistics = null;
                _identityId = null;
                _role = null;
                _ipPort = null;
                _lastActivity = DateTime.MinValue;
                _allowRequest = false;
                _lastAuth = DateTime.MinValue;
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
#endif
            }
        }

        #endregion

        public bool IsConnected()
        {
            if (_client == null || !_client.Connected || _dataStream == null)
            { 
                return false;
            }
            byte[] tmp = new byte[1];
            bool success = false;

            try
            {
                _writeLock.Wait();
                _client.Client.Send(tmp, 0, 0);
                success = true;
            }
            catch (SocketException se)
            {
                if (se.NativeErrorCode.Equals(10035)) success = true;
            }
            catch (Exception)
            {
            }
            finally
            {
                _writeLock?.Release();
            }

            if (success) return true;

            try
            {
                _writeLock.Wait();

                if (_client.Client.Poll(0, SelectMode.SelectWrite)
                    && !_client.Client.Poll(0, SelectMode.SelectError))
                {
                    byte[] buffer = new byte[1];
                    if (_client.Client.Receive(buffer, SocketFlags.Peek) == 0)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                _writeLock?.Release();
            }
        }

        private async Task HandleMessageAndReply(Message msg, Request request)
        {
            if (_events.OnRpcDataReceived == null) return;

            var response = await _events.HandleRpcReceived(request);
            if (response == null) return;

            StreamCommon.BytesToStream(response.Data, 0, out int contentLength, out Stream stream);
            Message respMsg = new(
                response.Header,
                contentLength,
                stream,
                MessageType.ResponsePack,
                msg.Expiration,
                msg.ConversationGuid);
            await SendInternalAsync(respMsg, contentLength, stream).ConfigureAwait(false);

            //#if NET40
            //            Task unawaited = TaskEx.Run(async () =>
            //            {
            //#else
            //            Task unawaited = Task.Run(async () =>
            //                        {       
            //#endif
            //                var response = await _events.HandleRpcReceived(request);
            //                if (response != null)
            //                {
            //                    StreamCommon.BytesToStream(response.Data, 0, out int contentLength, out Stream stream);
            //                    Message respMsg = new(
            //                        response.Header,
            //                        contentLength,
            //                        stream,
            //                        MessageType.ResponsePack,
            //                        msg.Expiration,
            //                        msg.ConversationGuid);
            //                    await SendInternalAsync(respMsg, contentLength, stream).ConfigureAwait(false);
            //                }
            //            }, _token);

            //        }
        }
    }
}
