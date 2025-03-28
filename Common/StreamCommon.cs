using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Kzone.Signal
{
    internal static class StreamCommon
    {
        internal static byte[] ReadStreamFully(Stream input)
        {
            byte[] buffer = new byte[65536];
            using (MemoryStream ms = new MemoryStream())
            {
                int read = 0;
                while (true)
                {
                    read = input.Read(buffer, 0, buffer.Length);
                    if (read > 0)
                    {
                        ms.Write(buffer, 0, read);
                    }
                    else
                    {
                        break;
                    }
                }

                return ms.ToArray();
            }
        }

        internal static byte[] ReadFromStream(Stream stream, long count, int bufferLen)
        {
            if (count <= 0) return new byte[0];
            if (bufferLen <= 0) throw new ArgumentException("Buffer must be greater than zero bytes.");
            byte[] buffer = new byte[bufferLen];

            int read = 0;
            long bytesRemaining = count;
            MemoryStream ms = new MemoryStream();

            while (bytesRemaining > 0)
            {
                if (bufferLen > bytesRemaining) buffer = new byte[bytesRemaining];

                read = stream.Read(buffer, 0, buffer.Length);
                if (read > 0)
                {
                    ms.Write(buffer, 0, read);
                    bytesRemaining -= read;
                }
                else
                {
                    throw new IOException("Could not read from supplied stream.");
                }
            }

            byte[] data = ms.ToArray();
            return data;
        }

        internal static MemoryStream DataStreamToMemoryStream(long contentLength, Stream stream, int bufferLen)
        {
            if (contentLength <= 0) return new MemoryStream(new byte[0]);
            if (bufferLen <= 0) throw new ArgumentException("Buffer must be greater than zero bytes.");
            byte[] buffer = new byte[bufferLen];

            int read = 0;
            long bytesRemaining = contentLength;
            MemoryStream ms = new MemoryStream();

            while (bytesRemaining > 0)
            {
                if (bufferLen > bytesRemaining) buffer = new byte[bytesRemaining];

                read = stream.Read(buffer, 0, buffer.Length);
                if (read > 0)
                {
                    ms.Write(buffer, 0, read);
                    bytesRemaining -= read;
                }
                else
                {
                    throw new IOException("Could not read from supplied stream.");
                }
            }

            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }


        internal static async Task<byte[]> ReadFromStreamAsync(Stream stream, long count, int bufferLen)
        {
            if (count <= 0) return new byte[0];
            if (bufferLen <= 0) throw new ArgumentException("Buffer must be greater than zero bytes.");

            // Tạo MemoryStream với dung lượng đã biết trước để tránh resize
            using MemoryStream ms = new((int)Math.Min(count, int.MaxValue));

            try
            {
                // Sử dụng kích thước buffer tối ưu
                byte[] buffer = new byte[Math.Min(bufferLen, 65536)];
                long bytesRemaining = count;
                int read;

                while (bytesRemaining > 0)
                {
                    // Điều chỉnh kích thước đọc dựa trên số byte còn lại
                    int bytesToRead = (int)Math.Min(bytesRemaining, buffer.Length);

                    read = await stream.ReadAsync(buffer, 0, bytesToRead).ConfigureAwait(false);
                    if (read > 0)
                    {
                        await ms.WriteAsync(buffer, 0, read).ConfigureAwait(false);
                        bytesRemaining -= read;
                    }
                    else
                    {
                        // Stream đã kết thúc sớm hơn dự kiến
                        throw new IOException("Could not read from supplied stream. Stream ended unexpectedly.");
                    }
                }
            }
            catch (Exception ex) when (!(ex is IOException))
            {
                // Log lỗi cụ thể nhưng cho phép các ngoại lệ ngắt kết nối bị bắt ở cấp cao hơn
                throw new IOException($"Error reading from stream: {ex.Message}", ex);
            }

            ms.Seek(0, SeekOrigin.Begin);
            return ms.ToArray();
        }


        internal static async Task<byte[]> ReadMessageDataAsync(Message msg, int bufferLen)
        {
            if (msg == null) throw new ArgumentNullException(nameof(msg));
            if (msg.ContentLength == 0) return new byte[0];

            byte[] msgData = null;

            try
            {
                msgData = await ReadFromStreamAsync(msg.DataStream, msg.ContentLength, bufferLen);
            }
            catch
            {
                // có thể xuất hiện ngoại lệ nếu client ngắt kết nối đột ngột
                // hoặc có thể xuất hiện ngay sau khi send
            }

            return msgData;
        }

        internal static byte[] AppendBytes(byte[] head, byte[] tail)
        {
            using var memoryStream = new MemoryStream();
            memoryStream.Write(head, 0, head.Length);
            memoryStream.Write(tail, 0, tail.Length);
            return memoryStream.ToArray();
        }

        internal static string ByteArrayToHex(byte[] data)
        {
            StringBuilder hex = new(data.Length * 2);
            foreach (byte b in data) hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        internal static void ObjectToStream(object obj, out int contentLength, out Stream stream)
        {
            contentLength = 0;
            stream = new MemoryStream();

            if (obj == null)
            {
                // Nếu object là null, trả về MemoryStream rỗng.
                return;
            }

            try
            {
                // Sử dụng hàm SerializeToStream để ghi trực tiếp vào stream
                obj.SerializeToStream(stream);

                // Cập nhật contentLength và reset vị trí stream
                contentLength = (int)stream.Length;
                stream.Seek(0, SeekOrigin.Begin);
            }
            catch (Exception ex)
            {
                // Đảm bảo stream rỗng trong trường hợp lỗi
                stream = new MemoryStream();
                contentLength = 0;
                throw new InvalidOperationException("Failed to serialize object", ex);
            }
        }

        internal static void BytesToStream(byte[] data, int start, out int contentLength, out Stream stream)
        {
            contentLength = 0;
            stream = new MemoryStream();

            if (data == null || data.Length == 0 || start >= data.Length)
            {
                // Nếu mảng rỗng hoặc start không hợp lệ, trả về MemoryStream rỗng.
                return;
            }

            contentLength = data.Length - start;

            // Viết dữ liệu vào stream nếu contentLength > 0
            if (contentLength > 0)
            {
                stream.Write(data, start, contentLength);
                stream.Seek(0, SeekOrigin.Begin);
            }
        }

       

        //internal static void BytesToStream(byte[] data, int start, out int contentLength, out Stream stream)
        //{
        //    contentLength = 0;
        //    if (data == null || data.Length == 0) stream = new MemoryStream(new byte[0]);
        //    else
        //    {
        //        contentLength = (data.Length - start);
        //        stream = new MemoryStream();
        //        stream.Write(data, start, contentLength);
        //        stream.Seek(0, SeekOrigin.Begin);
        //    }
        //}

        internal static DateTime GetExpirationTimestamp(Message msg)
        {
            DateTime expiration = msg.Expiration;

            //
            // TimeSpan will be negative if sender timestamp is earlier than now or positive if sender timestamp is later than now
            // Goal #1: if sender has a later timestamp, decrease expiration by the difference between sender time and our time
            // Goal #2: if sender has an earlier timestamp, increase expiration by the difference between sender time and our time
            // 
            // E.g. If sender time is 10:40 and receiver time is 10:45 and expiration is 1 minute, so 10:41.
            // ts = 10:45 - 10:40 = 5 minutes
            // expiration = 10:41 + 5 = 10:46 which is 1 minute later than when receiver received the message
            //
            TimeSpan ts = DateTime.UtcNow - msg.SenderTimestamp;
            expiration = expiration.AddMilliseconds(ts.TotalMilliseconds);

            return expiration;
        }
    }
}
