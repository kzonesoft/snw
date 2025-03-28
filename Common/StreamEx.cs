using System;
using System.IO;


namespace Kzone.Signal
{
    public class StreamEx : Stream
    {
        #region Public-Members


        public override bool CanRead => true;


        public override bool CanSeek => false;


        public override bool CanWrite => false;


        public override long Length
        {
            get
            {
                return _length;
            }
        }


        public override long Position
        {
            get
            {
                return _position;
            }
            set
            {
                throw new InvalidOperationException("Position may not be modified.");
            }
        }

        #endregion

        #region Private-Members



        #endregion

        #region Constructors-and-Factories

        internal StreamEx(long contentLength, Stream stream)
        {
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("Cannot read from supplied stream.");

            _length = contentLength;
            _stream = stream;
        }

        #endregion

        #region Public-Methods

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0) throw new ArgumentException("Offset must be zero or greater.");
            if (offset >= buffer.Length) throw new IndexOutOfRangeException("Offset must be less than the buffer length of " + buffer.Length + ".");
            if (count < 0) throw new ArgumentException("Count must be zero or greater.");
            if (count == 0) return 0;
            if (count + offset > buffer.Length) throw new ArgumentException("Offset and count must sum to a value less than the buffer length of " + buffer.Length + ".");

            lock (_lock)
            {
                byte[] temp = null;

                if (_bytesRemaining == 0) return 0;

                if (count > _bytesRemaining) temp = new byte[_bytesRemaining];
                else temp = new byte[count];

                int bytesRead = _stream.Read(temp, 0, temp.Length);
                Buffer.BlockCopy(temp, 0, buffer, offset, bytesRead);
                _position += bytesRead;

                return bytesRead;

            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException("Seek operations are not supported.");
        }


        public override void SetLength(long value)
        {
            throw new InvalidOperationException("Length may not be modified.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException("Stream is not writeable.");
        }

        #endregion

        private readonly object _lock = new object();
        private Stream _stream = null;
        private long _length = 0;
        private long _position = 0;
        private long _bytesRemaining
        {
            get
            {
                return _length - _position;
            }
        }
    }
}