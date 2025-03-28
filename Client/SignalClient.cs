using System;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace Kzone.Signal.Client
{
    public class SignalClient : ISignalClient, IDisposable
    {

        private IConnection _connection;
        private SignalConnectionState _state = SignalConnectionState.Disconnected;
        private bool _reconnectFlag = false;
        private Task _reconnectTask = null;
        private Events _events = new();
        private Settings _settings = new();
        private Statistics _statistics = new();
        private KeepaliveSettings _keepaliveSettings = new();
        private DebugLogger _debugLogger = new();
        private CancellationTokenSource _tokenSource = new();
        private readonly object _lockState = new();
        public string HostIp => _connection?.Host;
        public Settings Settings => _settings;
        public Events Events => _events;
        public Statistics Statistics => _statistics;
        public KeepaliveSettings KeepaliveSettings => _keepaliveSettings;
        public DebugLogger DebugLogger => _debugLogger;

        public SignalConnectionState State
        {
            get
            {
                lock (_lockState) return _state;
            }
        }

        public IConnection Connection => _connection;

        #region CTOR
        public SignalClient()
        {
            StartupRegister();
        }
        public SignalClient(Action<Settings> actionSetting)
        {
            actionSetting.Invoke(_settings);
            StartupRegister();
        }
        public SignalClient(Action<Settings> actionSetting, Action<DebugLogger> actionDebug)
        {
            actionSetting.Invoke(_settings);
            actionDebug.Invoke(_debugLogger);
            StartupRegister();
        }
        public SignalClient(Action<Settings> actionSetting, Action<DebugLogger> actionDebug, Action<KeepaliveSettings> actionKeepAlive)
        {
            actionSetting.Invoke(_settings);
            actionDebug.Invoke(_debugLogger);
            actionKeepAlive.Invoke(_keepaliveSettings);
            StartupRegister();
        }
        public SignalClient(string host, int port)
        {
            _settings.Host = host;
            _settings.Port = port;
            StartupRegister();
        }

        #endregion
        public void Connect()
        {
            //Console.WriteLine(Assembly.GetExecutingAssembly().FullName);
            try
            {
                //if (!PjCol.GetName(Assembly.GetEntryAssembly().FullName)) return;
                if (_reconnectTask != null)
                    throw new Exception("Reconnecting pending...");
                if (_state != SignalConnectionState.Disconnected)
                    throw new Exception("Network client connected");
                if (_settings.AutoReconnectSeconds > 0)
                {
#if NET40
                    _reconnectTask = TaskEx.Run(() => LoopReconnect(_tokenSource.Token), _tokenSource.Token);
#else
                    _reconnectTask = Task.Run(() => LoopReconnect(_tokenSource.Token), _tokenSource.Token);
#endif
                }
                InitializeTcpClient();
            }
            catch (Exception)
            {
                throw;
            }

        }

        public async Task ConnectAsync()
        {
#if NET40
            await TaskEx.Run(() => Connect(), _tokenSource.Token).ConfigureAwait(false);
#else
            await Task.Run(() => Connect(), _tokenSource.Token).ConfigureAwait(false);
#endif
        }



        public void Dispose()
        {
            try
            {
                _reconnectTask.StopIfRunning(_tokenSource);
                Disconnect();
            }
            finally
            {
                _tokenSource = null;
                _reconnectTask = null;
                _settings = null;
                _events = null;
                _statistics = null;
            }
        }
        public void Disconnect()
        {
            try
            {
                _connection?.Disconnect();
                _connection?.Dispose();
                _connection = null;
            }
            catch
            {
                _connection = null;
            }
        }

        private void InitializeTcpClient()
        {
            try
            {
                _connection = new Connection(_events, _settings, _statistics, _keepaliveSettings, _debugLogger);
                _connection.Connect();
            }
            catch (ArgumentException e)
            {
#if DEBUG
                _debugLogger.ExceptionRecord?.Invoke(e);
#endif
                return;
            }
            catch (AuthenticationException e)
            {
#if DEBUG
                _debugLogger.ExceptionRecord?.Invoke(e);
#endif
                return;
            }
            catch (Exception e)
            {
                if (e is ArgumentNullException)
                {
                    _debugLogger.ExceptionRecord?.Invoke(e);
                }
                _state = SignalConnectionState.Disconnected;
                _reconnectFlag = true;
            }
        }

        private void StartupRegister()
        {
            _events.ConnectionStateNotify += (s, e) =>
            {
                lock (_lockState)
                {
                    _state = e.Status;
                    if (e.Status == SignalConnectionState.Disconnected)
                    {
                        _reconnectFlag = true;
                    }
                }
            };
        }

        private async Task LoopReconnect(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
#if NET472 || NET6_0_OR_GREATER
                    await Task.Delay(_settings.AutoReconnectSeconds * 1000, token);
#elif NET40
                    await TaskEx.Delay(_settings.AutoReconnectSeconds * 1000, token);
#endif
                    if (!_reconnectFlag) continue; //check
                    Disconnect();
#if NET472 || NET6_0_OR_GREATER
                    await Task.Delay(TimeSpan.FromSeconds(5), token); //wait another exception if exits
#elif NET40
                    await TaskEx.Delay(TimeSpan.FromSeconds(5), token); //chờ có xuất hiện exception khác hay ko
#endif
                    if (_state != SignalConnectionState.Reconnecting)
                    {
                        _state = SignalConnectionState.Reconnecting;
                    }
                    if (_reconnectFlag)
                    {
                        _reconnectFlag = false;
                    }

                    InitializeTcpClient();
                }
                catch (Exception e)
                {
                    _debugLogger.ExceptionRecord?.Invoke(e);
                }
            }
        }


    }
}
