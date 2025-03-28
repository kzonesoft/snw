using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Kzone.Signal.Server
{
    public class ClientManager : IDisposable, IClientManager
    {
        private readonly ConcurrentDictionary<string, IClient> _clients = new();
        private readonly IdentitySessions _identitySessions = new();
        private Events _events;
        private Settings _settings;
        private DebugLogger _debugLogger;
        private CancellationToken _token;
        private int _totalConnections = 0;
        public int TotalConnections => _totalConnections;

        public ClientManager(Events events, Settings settings, DebugLogger debugLogger, CancellationToken token)
        {
            _events = events;
            _settings = settings;
            _debugLogger = debugLogger;
            _token = token;
            _events.OnClientDisconnected += (s, e) => RemoveClient(e.IpPort);
            MonitorIdleClient();
        }

        //Thêm mới client khi kết nối thành công
        public void AddClient(IClient client)
        {
            if (_clients.TryAdd(client.IpPort, client))
            {
                ((Client)client).BeginReceiver();
                Interlocked.Increment(ref _totalConnections);
                _events.HandleClientConnected(this, client.IpPort);
                _debugLogger.Logger?.Invoke(Severity.Debug, nameof(ClientManager) + " accepted connection from " + client.IpPort);
            }
            else
            {
                client.Disconnect(MessageType.Failure, true);
                _debugLogger.Logger?.Invoke(Severity.Debug, nameof(ClientManager) + " reject connection because add to manager failure " + client.IpPort);
            }
        }

        public bool IdentityAuthenticate(string identityId, IClient client)
        {
            bool isAuthenticated = false;

            // 1. Đảm bảo rằng session tồn tại. Nếu chưa có, tạo mới với thông tin từ client.
            // 2. Cập nhật session một cách thread-safe thông qua hàm Validate.

            _identitySessions.ValidateOrAdd(
                identityId,
                () => new SessionItem
                {
                   ConnectionId = client.Id,
                   IdentityId = identityId,
                   IpPort = client.IpPort,
                   IpOnly = client.IpOnly,
                   Expires = DateTime.Now.AddMinutes(10),
                   ForcedSessionUpdate = DateTime.MinValue
                },
                session =>
                {
                    DateTime currentTime = DateTime.Now;

                    // Nếu IP của session trùng với IP của client thì chỉ cần gia hạn thời gian hết hạn.
                    if (session.IpOnly == client.IpOnly)
                    {
                        session.Expires = currentTime.AddMinutes(10);
                        isAuthenticated = true;
                    }
                    // Nếu IP không khớp và session đã hết hạn hoặc đã vượt qua thời điểm cho phép cập nhật,
                    // thực hiện cập nhật toàn bộ thông tin của session.
                    else if (currentTime > session.Expires || currentTime > session.ForcedSessionUpdate)
                    {
                        // Tìm client cũ theo IpPort và vô hiệu hóa nếu tồn tại.
                        IClient oldClient = GetClientByIp(session.IpPort);
                        if (oldClient != null)
                        {
                            oldClient.IdentityId = string.Empty;
                            oldClient.AllowRequest = false;
                        }

                        // Cập nhật session với thông tin mới từ client hiện tại.
                        session.ConnectionId = client.Id;
                        session.IdentityId = identityId;
                        session.IpPort = client.IpPort;
                        session.IpOnly = client.IpOnly;
                        session.Expires = currentTime.AddMinutes(10);

                        // Cập nhật ForcedSessionUpdate nếu thời điểm hiện tại đã vượt qua giá trị cũ.
                        if (currentTime > session.ForcedSessionUpdate)
                        {
                            session.ForcedSessionUpdate = currentTime.AddMinutes(30);
                        }

                        isAuthenticated = true;
                    }
                    // Nếu không thỏa điều kiện nào, session giữ nguyên và isAuthenticated vẫn là false.
                });
            return isAuthenticated;
        }


        public void RemoveIdentitySession(string identityId)
        {
            _identitySessions.Remove(identityId);
        }


        //lấy tất cả danh sách client
        public IEnumerable<IClient> GetAllClient()
        {
            return _clients.Values;
        }

        //lấy client theo ipport
        public IClient GetClientByIp(string ipPort)
        {
            if (_clients.TryGetValue(ipPort, out IClient client))
            {
                return client;
            }
            return null;
        }

        //lấy client theo identityId
        public IClient GetClientByIdentityId(string identityId)
        {
            var session = _identitySessions.GetSession(identityId);
            if (session == null) return null;
            return _clients.TryGetValue(session.IpPort, out IClient client) ? client : null;
        }

        //lấy 1 client
        public IClient GetClient(Func<IClient, bool> filter)
        {
            return _clients.FirstOrDefault(x => filter(x.Value)).Value;
        }

        //lấy nhiều client 
        public IEnumerable<IClient> GetClients(Func<IClient, bool> filter)
        {
            return _clients.Where(x => filter(x.Value)).Select(z => z.Value);
        }

        //lấy enumerator
        public IEnumerator<KeyValuePair<string, IClient>> ToEnumerator()
        {
            return _clients.GetEnumerator();
        }


        //ngắt kết nối toàn bộ
        public void DisconnectAll()
        {
            var clients = _clients.GetEnumerator();
            while (clients.MoveNext())
            {
                clients.Current.Value?.Disconnect();
            }
        }

        public void DisconnectByIp(string ipPort)
        {
            DisconnectClient(ip: ipPort);
        }

        public void DisconnectByIdentityId(string identityId)
        {
            DisconnectClient(identity: identityId);
        }

        private void DisconnectClient(string ip = null, string identity = null)
        {
            IClient client = null;

            if (!string.IsNullOrWhiteSpace(ip))
            {
                client = GetClientByIp(ip);
            }
            else if (!string.IsNullOrWhiteSpace(identity))
            {
                client = GetClientByIdentityId(identity);
            }

            if (client != null)
            {
                try
                {
                    client.Disconnect();
                }
                catch
                {
                    RemoveClient(client.IpPort);
                }
            }
        }

        private void RemoveClient(string ipPort)
        {
            if (_clients.TryRemove(ipPort, out IClient client))
            {
                if (!string.IsNullOrEmpty(client.IdentityId))
                {
                    _identitySessions.Remove(client.IdentityId);
                }
                _debugLogger.Logger?.Invoke(Severity.Debug, nameof(ClientManager) + " remove client " + ipPort + " success");
                Interlocked.Decrement(ref _totalConnections);
            }
            else
            {
                _debugLogger.Logger?.Invoke(Severity.Debug, nameof(ClientManager) + " remove client " + ipPort + "from loginManager failure");
            }
        }




        public void Dispose()
        {
            _clients.Clear();
            _events = null;
            _settings = null;
            _settings = null;
        }



        private void MonitorIdleClient()
        {
#if NET40
            TaskEx.Run
#else
            Task.Run
#endif
           (async () =>
           {
               while (!_token.IsCancellationRequested)
               {
#if NET40
                   await TaskEx.Delay
#else
                   await Task.Delay
#endif
                      (5000, _token).ConfigureAwait(false);

                   if (_settings?.IdleClientTimeoutSeconds > 0)
                   {
                       try
                       {
                           foreach (var client in _clients)
                           {
                               if (client.Value == null)
                               {
                                   _clients.TryRemove(client.Key, out _);
                                   continue;
                               }
                               if (client.Value.CalculatorLastActivity() > _settings.IdleClientTimeoutSeconds)
                               {
                                   _clients.TryRemove(client.Key, out _);
                                   client.Value.Dispose();
                               }
                           }
                       }
                       catch (Exception e)
                       {
                           _debugLogger.Logger?.Invoke(Severity.Info, $"Disconnect client by monitor idle task failure with message : {e.Message}");
                       }
                   }
               };
           }, _token);
        }


    }
}
