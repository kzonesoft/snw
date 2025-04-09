using System;

namespace Kzone.Engine.Controller.Domain.Exceptions
{
    public class ServiceTimeoutException : Exception
    {
        public ServiceTimeoutException() { }
        public ServiceTimeoutException(string message) : base(message) { }
    }
}
