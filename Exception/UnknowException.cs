using System;

namespace Kzone.Signal
{
    public class UnknowException : Exception
    {
        public UnknowException() { }
        public UnknowException(string message) : base(message) { }
    }
}
