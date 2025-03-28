using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;


namespace Kzone.Signal.Server
{
    public class SignalServer : IDisposable, ISignalServer
    {

        private bool _isListening = false;
        private TcpListener _listener;
        private IClientManager _clientManager;
        private CancellationTokenSource _tokenSource = new();
        private CancellationToken _token;
        private Task _acceptConnections = null;
        private Settings _settings = new();
        private Events _events = new();
        private Statistics _statistics = new();
        private DebugLogger _debugLogger = new();
        private KeepaliveSettings _keepaliveSettings = new();

        public bool IsListening => _isListening;
        public Settings Settings => _settings;
        public Events Events => _events;
        public Statistics Statistics => _statistics;
        public DebugLogger DebugLogger => _debugLogger;
        public KeepaliveSettings KeepaliveSettings => KeepaliveSettings;
        public IClientManager ClientManager => _clientManager;

        public SignalServer() { }

        /// <summary>
        /// ADC - Kzonesoft
        /// Sử dụng lambda để config. khuyến nghị nên dùng constructor này
        /// </summary>
        /// <param name="actionSetting"></param>
        public SignalServer(Action<Settings> actionSetting)
        {
            actionSetting.Invoke(_settings);
        }

        /// <summary>
        /// ADC - Kzonesoft
        /// Không khuyến nghị dùng constructor này
        /// </summary>
        /// <param name="actionSetting"></param>
        public SignalServer(string listenerIp, int listenerPort)
        {
            _settings.SetListenner(listenerIp, listenerPort);
        }


        public Task StartAsync()
        {
#if NET40
            return TaskEx.Run(() => Start());
#else
            return Task.Run(() => Start());
#endif
        }


        public void Start()
        {
            if (_isListening) throw new InvalidOperationException("KzoneServer is already running.");
            _tokenSource = new CancellationTokenSource();
            _token = _tokenSource.Token;
            _clientManager ??= new ClientManager(_events, _settings, _debugLogger, _token);
            _listener = new TcpListener(_settings.ListenIp, _settings.ListenPort)
            {
                ExclusiveAddressUse = true
            };
            _debugLogger.Logger?.Invoke(Severity.Info, nameof(SignalServer) + " starting on " + _settings.ListenIp.ToString() + ":" + _settings.ListenPort.ToString());

            _listener.Start();
#if NET40
            _acceptConnections = TaskEx.Run(() => AcceptConnections(), _token); // sets _IsListening
#else
            _acceptConnections = Task.Run(() => AcceptConnections(), _token); // sets _IsListening
#endif
            Events.HandleServerStarted(this);
        }


        public void Stop()
        {
            if (!_isListening) throw new InvalidOperationException("KzoneServer is not running.");

            try
            {
                _isListening = false;
                _listener.Stop();
                _tokenSource.Cancel();

                _debugLogger.Logger?.Invoke(Severity.Info, nameof(SignalServer) + " stopped");
                Events.HandleServerStopped(this);
            }
            catch (Exception e)
            {
                //Events.HandleExceptionEncountered(this, e);
                _debugLogger.ExceptionRecord?.Invoke(e);
                throw;
            }
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _debugLogger.Logger?.Invoke(Severity.Info, nameof(SignalServer) + " disposing");

                if (_isListening) Stop();

                _clientManager.DisconnectAll();

                if (_listener != null)
                {
                    if (_listener.Server != null)
                    {
                        _listener.Server.Close();
                        _listener.Server.Dispose();
                    }
                }
                _listener = null;
                _tokenSource = null;
                _acceptConnections = null;
                _isListening = false;
                _clientManager?.Dispose();
            }
        }

        private async Task AcceptConnections()
        {
            _isListening = true;
            //if (!PjCol.GetName(Assembly.GetEntryAssembly().FullName)) return;

            while (!_token.IsCancellationRequested)
            {
                try
                {
                    if (!_isListening && _clientManager.TotalConnections >= Settings.MaxConnections)
                    {
#if NET40
                        await TaskEx.Delay(50).ConfigureAwait(false);
#else
                        await Task.Delay(50).ConfigureAwait(false);
#endif
                        continue;
                    }
                    else if (!_isListening)
                    {
                        _listener.Start();
                        _isListening = true;
                    }
                    TcpClient tcpClient = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    tcpClient.LingerState.Enabled = false;
                    tcpClient.NoDelay = _settings.NoDelay;


                    string clientIp = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address.ToString();
                    // Filter out unpermitted or blocked IPs
                    if ((_settings.PermittedIPs.Count > 0 && !_settings.PermittedIPs.Contains(clientIp)) ||
                        (_settings.BlockedIPs.Count > 0 && _settings.BlockedIPs.Contains(clientIp)))
                    {
                        _debugLogger.Logger?.Invoke(Severity.Info, $"{nameof(SignalServer)} rejecting connection from {clientIp}");
                        tcpClient.Close();
                        continue;
                    }

#if NET40
                    _ = TaskEx.Run(() => InitializeNewClientContext(tcpClient), _token);
#else
                    _ = Task.Run(() => InitializeNewClientContext(tcpClient), _token);
#endif
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception e)
                {
                    _debugLogger.Logger?.Invoke(Severity.Error,
                        nameof(SignalServer) + " listener exception: " +
                        Environment.NewLine +
                        e.Message +
                        Environment.NewLine);
                    break;
                }
            }
        }

        private void InitializeNewClientContext(TcpClient tcpClient)
        {
            IClient client = new Client(tcpClient, _events, _settings, _statistics, _keepaliveSettings, _debugLogger);
            _clientManager.AddClient(client);

            if (_clientManager.TotalConnections >= _settings.MaxConnections)
            {
                _debugLogger.Logger?.Invoke(Severity.Info, nameof(SignalServer) + " maximum connections " + Settings.MaxConnections + " met (currently " + _clientManager.TotalConnections + " connections), pausing");
                _isListening = false;
                _listener.Stop();
            }
        }

    }
}
