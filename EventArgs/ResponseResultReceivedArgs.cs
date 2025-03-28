using System;

namespace Kzone.Signal
{
    /// <summary>
    /// Response nhận được ở đầu gửi chờ broadcast lại cho phương thức SendWaitResponse
    /// </summary>
    internal class ResponseResultReceivedArgs : EventArgs
    {
        public Message Message { get; set; } = null;
        public byte[] BytesData { get; set; } = null;

        public ResponseResultReceivedArgs(Message msg, byte[] data)
        {
            Message = msg;
            BytesData = data;
        }
    }
}
