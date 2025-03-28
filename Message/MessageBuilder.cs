
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Kzone.Signal
{
    internal class MessageBuilder
    {
        private readonly static byte[] headerKey = new byte[3] { 0xA7, 0x3E, 0x8F };

        internal static byte[] GetHeaderBytes(Message msg)
        {

            // Chuyển đổi message sang mảng byte
            byte[] mainBytes;
            try
            {
                mainBytes = msg.ToHeaderBytes(headerKey);
            }
            catch (Exception ex)
            {
                // Xử lý ngoại lệ nếu quá trình serializing thất bại
                throw new InvalidOperationException("Failed to convert message to header bytes.", ex);
            }

            // Thêm chuỗi ký tự "\r\n\r\n" vào cuối message
            byte[] end = Encoding.UTF8.GetBytes("\r\n\r\n");

            // Kết hợp mainBytes và end thành một mảng byte duy nhất
            byte[] final = new byte[mainBytes.Length + end.Length];
            Buffer.BlockCopy(mainBytes, 0, final, 0, mainBytes.Length);
            Buffer.BlockCopy(end, 0, final, mainBytes.Length, end.Length);

            return final;
        }
        internal static async Task<Message> BuildFromStream(Stream stream, CancellationToken token)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("Cannot read from stream.");

            // Đọc tiêu đề ban đầu
            int firstSize = 16;
            byte[] headerBytes = new byte[firstSize];
            int bytesRead = 0;
            while (bytesRead < firstSize)
            {
                int read = await stream.ReadAsync(headerBytes, bytesRead, firstSize - bytesRead, token).ConfigureAwait(false);
                if (read == 0) throw new IOException("Stream closed unexpectedly.");
                bytesRead += read;
            }

            byte[] headerBuffer = new byte[1];

            // Vòng lặp để đọc tiếp dữ liệu từ stream và kiểm tra chuỗi kết thúc
            while (true)
            {
                if (headerBytes[headerBytes.Length - 1] == 10
                    && headerBytes[headerBytes.Length - 2] == 13
                    && headerBytes[headerBytes.Length - 3] == 10
                    && headerBytes[headerBytes.Length - 4] == 13)
                {
                    break;
                }

                // Đọc thêm một byte từ stream
                int additionalBytesRead = await stream.ReadAsync(headerBuffer, 0, 1, token).ConfigureAwait(false);
                if (additionalBytesRead == 0)
                {
                    throw new IOException("Stream closed unexpectedly while reading header.");
                }

                // Gắn byte vừa đọc vào headerBytes
                headerBytes = StreamCommon.AppendBytes(headerBytes, headerBuffer);
            }

            // Deserialize message
            Message msg;
            try
            {
                msg = headerBytes.ToMessage(headerKey);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to deserialize message from stream.", ex);
            }

            // Liên kết stream với Message
            msg.DataStream = stream;
            return msg;
        }

        //internal static async Task<Message> BuildFromStream(Stream stream, CancellationToken token)
        //{
        //    if (stream == null) throw new ArgumentNullException(nameof(stream));
        //    if (!stream.CanRead) throw new ArgumentException("Cannot read from stream.");

        //    // {"len":0,"s":"Normal"}\r\n\r\n
        //    byte[] headerBytes = new byte[24];
        //    await stream.ReadAsync(headerBytes, 0, 24, token).ConfigureAwait(false);
        //    byte[] headerBuffer = new byte[1];
        //    while (true)
        //    {
        //        byte[] endCheck = headerBytes.Skip(headerBytes.Length - 4).Take(4).ToArray();

        //        if (endCheck[3] == 0
        //           && endCheck[2] == 0
        //           && endCheck[1] == 0
        //           && endCheck[0] == 0)
        //        {
        //            throw new IOException("Null header data indicates peer disconnected.");
        //        }

        //        if (endCheck[3] == 10
        //            && endCheck[2] == 13
        //            && endCheck[1] == 10
        //            && endCheck[0] == 13)
        //        {
        //            break;
        //        }

        //        await stream.ReadAsync(headerBuffer, 0, 1, token).ConfigureAwait(false);
        //        headerBytes = StreamCommon.AppendBytes(headerBytes, headerBuffer);
        //    }
        //    //var msg = headerBytes.Take(headerBytes.Length - 4).ToArray().Deserialize<Message>();
        //    var msg = headerBytes.ToMessage(headerKey);
        //    msg.DataStream = stream;
        //    return msg;
        //}
    }
}
