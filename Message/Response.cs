using System;


namespace Kzone.Signal
{
    /// <summary>
    /// Trả gói Response về cho phương thức SendWaitResponse
    /// </summary>
    public class Response
    {
        private object _baseData;
        public Header Header { get; }
        public byte[] BytesData => _baseData.Serialize();
        public object DefaultData => _baseData;

        internal DateTime ExpirationUtc { get; set; }
     
        public Response(Header header, object data)
        {
            _baseData ??= new byte[0];
            Header = header;
            _baseData = data;
        
        }
        //reply => task rtcsend
        internal Response(DateTime expirationUtc, Header headerPacket, byte[] data)
        {
            ExpirationUtc = expirationUtc;
            Header = headerPacket;
            _baseData = data;
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
