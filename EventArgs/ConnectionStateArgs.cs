using System;


namespace Kzone.Signal
{
    public class ConnectionStateArgs : EventArgs
    {
        public SignalConnectionState Status { get; private set; }
        public ConnectionStateArgs(SignalConnectionState status)
        {
            Status = status;
        }
    }
}
