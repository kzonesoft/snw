using System;
using System.Runtime.Serialization;

namespace Kzone.Engine.Controller.Domain.Exceptions
{
#if !PORTABLE
    [Serializable]
#endif
    public class InvalidCredentialException : TorrentException
    {
        public InvalidCredentialException() { }
        public InvalidCredentialException(string message) : base(message) { }
        public InvalidCredentialException(string message, Exception innerException) : base(message, innerException) { }
#if !PORTABLE
        protected InvalidCredentialException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
    }
}
