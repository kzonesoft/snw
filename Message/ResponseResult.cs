using System;


namespace Kzone.Signal
{
    /// <summary>
    /// Trả gói Response về cho phương thức SendWaitResponse
    /// </summary>
    public class ResponseResult
    {
        public Header Header { get; }
        public byte[] Data { get; }
        internal DateTime ExpirationUtc { get; set; }
        //received OK => reply
        public ResponseResult(Header header, byte[] data)
        {
            Header = header;
            Data = data;
        }
        public ResponseResult(Header header, object data)
        {
            data ??= new byte[0];
            Header = header;
            Data = data.Serialize();
        }
        //reply => task rtcsend
        internal ResponseResult(DateTime expirationUtc, Header headerPacket, byte[] data)
        {
            ExpirationUtc = expirationUtc;
            Header = headerPacket;
            Data = data;
        }
    }
    //rtcsend => result<T>
    public class ResponseResult<T>
    {
        public ResponseStatusCode StatusCode { get; private set; }
        public T Data { get; private set; }

        public ResponseResult(ResponseStatusCode status, T data)
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
