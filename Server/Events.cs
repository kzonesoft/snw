using Kzone.Signal.Base;
using System;

namespace Kzone.Signal.Server
{
    public class Events : BaseEvent
    {
        public event EventHandler<AuthenticationSucceededArgs> OnAuthenticationSucceeded;

        public event EventHandler<AuthenticationFailedArgs> OnAuthenticationFailed;

        public event EventHandler<ConnectedArgs> OnClientConnected;

        public event EventHandler<DisconnectionArgs> OnClientDisconnected;

        public event EventHandler OnServerStarted;

        public event EventHandler OnServerStopped;


        internal void HandleAuthenticationSucceeded(object sender, string ipPort)
        {
            OnAuthenticationSucceeded?.Invoke(sender, new AuthenticationSucceededArgs(ipPort));
        }

        internal void HandleAuthenticationFailed(object sender, string ipPort)
        {
            OnAuthenticationFailed?.Invoke(sender, new AuthenticationFailedArgs(ipPort));
        }

        internal void HandleClientConnected(object sender, string ipPort)
        {
            OnClientConnected?.Invoke(sender, new ConnectedArgs(ipPort));
        }

        internal void HandleClientDisconnected(object sender, string ipPort, DisconnectReason reason)
        {
            OnClientDisconnected?.Invoke(sender, new DisconnectionArgs(ipPort, reason));
        }

        internal void HandleServerStarted(object sender)
        {
            OnServerStarted?.Invoke(sender, EventArgs.Empty);
        }

        internal void HandleServerStopped(object sender)
        {
            OnServerStopped?.Invoke(sender, EventArgs.Empty);
        }
    }
}
