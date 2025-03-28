using System;

namespace Kzone.Signal.Client
{
    public class Events : BaseEvent
    {
        public event EventHandler OnAuthenticationSucceeded;

        public event EventHandler OnAuthenticationFailure;

        public event EventHandler<ConnectedArgs> OnServerHandshake;

        public event EventHandler<ConnectedArgs> OnServerConnected;

        public event EventHandler<DisconnectionArgs> OnServerDisconnected;

        internal event EventHandler<ConnectionStateArgs> ConnectionStateNotify;

        internal void HandleAuthenticationSucceeded(object sender)
        {
            OnAuthenticationSucceeded?.Invoke(sender, EventArgs.Empty);
        }

        internal void HandleAuthenticationFailure(object sender)
        {
            OnAuthenticationFailure?.Invoke(sender, EventArgs.Empty);
        }

        internal void HandleServerHandShake(object sender, string ipPort)
        {
            OnServerHandshake?.Invoke(sender, new ConnectedArgs(ipPort));
        }
        internal void HandleServerConnected(object sender, string ipPort)
        {
            OnServerConnected?.Invoke(sender, new ConnectedArgs(ipPort));
        }

        internal void HandleServerDisconnected(object sender, string ipPort, DisconnectReason reason)
        {
            OnServerDisconnected?.Invoke(sender, new DisconnectionArgs(ipPort, reason));
        }

        internal void HandleConnectionState(object sender, SignalConnectionState status)
        {
            ConnectionStateNotify?.Invoke(sender, new ConnectionStateArgs(status));
        }
    }
}
