using System.Threading.Tasks;

namespace Kzone.Signal.Server
{
    public interface ISignalServer
    {
        IClientManager ClientManager { get; }
        Events Events { get; }
        bool IsListening { get; }
        Settings Settings { get; }
        Statistics Statistics { get; }
        DebugLogger DebugLogger { get; }

        void Dispose();
        void Start();
        Task StartAsync();
        void Stop();
    }
}