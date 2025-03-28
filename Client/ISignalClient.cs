using System.Threading.Tasks;

namespace Kzone.Signal.Client
{
    public interface ISignalClient
    {
        string HostIp { get; }
        SignalConnectionState State { get; }
        Events Events { get; }
        Settings Settings { get; }
        Statistics Statistics { get; }
        KeepaliveSettings KeepaliveSettings { get; }
        DebugLogger DebugLogger { get; }
        IConnection Connection { get; }

        void Connect();
        Task ConnectAsync();
        void Disconnect();
        void Dispose();
    }
}