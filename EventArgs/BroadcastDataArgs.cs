
using System;

namespace Kzone.Signal
{
    public class BroadcastDataArgs : EventArgs
    {
        public Header Header { get; }
        public byte[] BytesData { get; }
        public BroadcastDataArgs(Header header, byte[] data)
        {
            Header = header;
            BytesData = data;
        }
        public T Data<T>()
        {
            if (BytesData == null)
                throw new NullReferenceException("data bytes null");
            return BytesData.Deserialize<T>();
        }
    }
}
