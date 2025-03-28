
using System;

namespace Kzone.Signal
{
    public class IpPortArgsBase : EventArgs
    {
        public string IpPort { get; }

        internal IpPortArgsBase(string ipPort)
        {
            IpPort = ipPort;
        }
    }
    public class ConnectedArgs : IpPortArgsBase
    {
        internal ConnectedArgs(string ipPort) : base(ipPort) { }
    }

    public class DisconnectionArgs : IpPortArgsBase
    {
        public DisconnectReason Reason { get; }
        internal DisconnectionArgs(string ipPort, DisconnectReason reason) : base(ipPort)
        {
            Reason = reason;
        }
    }

    public class AuthenticationSucceededArgs : IpPortArgsBase
    {
        internal AuthenticationSucceededArgs(string ipPort) : base(ipPort) { }
    }

    public class AuthenticationFailedArgs : IpPortArgsBase
    {
        internal AuthenticationFailedArgs(string ipPort) : base(ipPort) { }
    }


}

