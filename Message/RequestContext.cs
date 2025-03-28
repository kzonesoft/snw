using System;



namespace Kzone.Signal
{
    public class RequestContext
    {
        public object Sender { get; }
        public DateTime ExpirationUtc { get; }
        public Header Header { get; }
        public byte[] BytesData { get; }
        internal string ConversationGuid { get; }
        /// <summary>
        /// Dữ liệu nhận được ở đầu OnReceivedAndResponse
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="convGuid"></param>
        /// <param name="expirationUtc"></param>
        /// <param name="header"></param>
        /// <param name="byteData"></param>
        internal RequestContext(object sender, string convGuid, DateTime expirationUtc, Header header, byte[] byteData)
        {
            Sender = sender;
            ConversationGuid = convGuid;
            ExpirationUtc = expirationUtc;
            Header = header;

            if (byteData != null)
            {
                BytesData = new byte[byteData.Length];
                Buffer.BlockCopy(byteData, 0, BytesData, 0, byteData.Length);
            }
        }

        public T Data<T>()
        {
            if (BytesData == null)
                throw new NullReferenceException("data bytes null");
            return BytesData.Deserialize<T>();
        }

    }
}
