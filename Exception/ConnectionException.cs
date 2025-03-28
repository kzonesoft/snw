using System;


namespace Kzone.Signal
{
    public class ConnectionException : Exception
    {
        public ConnectionException() { }
        public ConnectionException(string message) : base(message) { }
    }
}
