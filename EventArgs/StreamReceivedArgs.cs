
using System;
using System.IO;

namespace Kzone.Signal
{
    public class StreamReceivedArgs : EventArgs
    {

        public Header Header => _PacketHeader;
        public long ContentLength => _ContentLength;
        public Stream DataStream => _DataStream;


        internal StreamReceivedArgs(Header header, long contentLength, Stream stream)
        {
            _PacketHeader = header;
            _ContentLength = contentLength;
            _DataStream = stream;
        }

        public byte[] Data
        {
            get
            {
                if (_Data != null) return _Data;
                if (_ContentLength <= 0) return null;
                _Data = ReadFromStream(_DataStream, _ContentLength);
                return _Data;
            }
        }

        private byte[] ReadFromStream(Stream stream, long count)
        {
            if (count <= 0) return new byte[0];
            byte[] buffer = new byte[_BufferSize];

            int read = 0;
            long bytesRemaining = count;
            MemoryStream ms = new();

            while (bytesRemaining > 0)
            {
                if (_BufferSize > bytesRemaining) buffer = new byte[bytesRemaining];

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

        private Header _PacketHeader;
        private long _ContentLength;
        private Stream _DataStream;
        private byte[] _Data = null;
        private int _BufferSize = 65536;

    }
}
