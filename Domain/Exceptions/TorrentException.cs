using System;
#if !PORTABLE
using System.Runtime.Serialization;
#endif

namespace Kzone.Engine.Controller.Domain.Exceptions
{
#if !PORTABLE
    [Serializable]
#endif
    public class TorrentException : Exception
    {
        public TorrentException() { }
        public TorrentException(string message) : base(message) { }
        public TorrentException(string message, Exception innerException) : base(message, innerException) { }
#if !PORTABLE
        protected TorrentException(SerializationInfo info, StreamingContext context) : base(info, context) { }
#endif
    }
}
