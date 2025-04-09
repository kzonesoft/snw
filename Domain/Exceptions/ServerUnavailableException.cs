using System;
using System.Runtime.Serialization;

namespace Kzone.Engine.Controller.Domain.Exceptions
{
#if !PORTABLE
    [Serializable]
#endif
    public class ServerUnavailableException : TorrentException
    {
        public ServerUnavailableException() { }
        public ServerUnavailableException(string message) : base(message) { }
        public ServerUnavailableException(string message, Exception innerException) : base(message, innerException) { }
#if !PORTABLE
        protected ServerUnavailableException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
    }
}
