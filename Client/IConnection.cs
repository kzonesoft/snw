using Kzone.Signal.Base;
using System;

namespace Kzone.Signal.Client
{
    public interface IConnection : INetworkContext, IDisposable
    {
        string Host { get; }
        bool IsConnected();
        void Connect();
        void Disconnect(bool sendNotice = true);
    }
}
