using System;
using System.IO;
using System.Threading.Tasks;

namespace Kzone.Signal.Base
{
    public class BaseEvent
    {
        public event EventHandler<BroadcastDataArgs> OnBroadcastData;

        public event EventHandler<StreamReceivedArgs> OnStreamReceived;
        public Func<Request, Task<Response>> OnRpcRequestData { get; set; } = null;

        internal void HandleDataReceived(object sender, Header headerPacket, byte[] data)
        {
            OnBroadcastData?.Invoke(sender, new BroadcastDataArgs(headerPacket, data));
        }

        internal void HandleStreamReceived(object sender, Header headerPacket, long contentLength, Stream stream)
        {
            OnStreamReceived?.Invoke(sender, new StreamReceivedArgs(headerPacket, contentLength, stream));
        }

        internal Task<Response> HandleRpcReceived(Request req)
        {
            if (OnRpcRequestData != null)
            {
                try
                {
                    return OnRpcRequestData(req);
                }
                catch (Exception) { return default; }
            }
            return default;
        }
    }
}
