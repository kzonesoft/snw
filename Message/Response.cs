using System;


namespace Kzone.Signal
{
    /// <summary>
    /// Trả gói Response về cho phương thức SendWaitResponse
    /// </summary>
    public class Response
    {
        public Header Header { get; }
        public byte[] Data { get; }
        internal DateTime ExpirationUtc { get; set; }
        //received OK => reply
        public Response(Header header, byte[] data)
        {
            Header = header;
            Data = data;
        }
        public Response(Header header, object data)
        {
            data ??= new byte[0];
            Header = header;
            Data = data.Serialize();
        }
        //reply => task rtcsend
        internal Response(DateTime expirationUtc, Header headerPacket, byte[] data)
        {
            ExpirationUtc = expirationUtc;
            Header = headerPacket;
            Data = data;
        }
    }
    //rtcsend => result<T>
    public class Response<T>
    {
        public ResponseStatusCode StatusCode { get; private set; }
        public T Data { get; private set; }

        public Response(ResponseStatusCode status, T data)
        {
            StatusCode = status;
            Data = data;
        }

        public bool IsSuccessStatus
        {
            get
            {
                return StatusCode == ResponseStatusCode.Ok
                    || StatusCode == ResponseStatusCode.Accept;
            }
        }
    }
}
