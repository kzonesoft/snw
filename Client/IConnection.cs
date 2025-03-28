using System;

namespace Kzone.Signal.Client
{
    public interface IConnection : IBaseClientContext, IDisposable
    {
        string Host { get; }
        bool Connected { get; }
        void Connect();
        void Disconnect(bool sendNotice = true);
    }
}
