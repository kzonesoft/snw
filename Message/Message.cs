using ProtoBuf;
using System;
using System.ComponentModel;
using System.IO;

namespace Kzone.Signal
{
    [ProtoContract(SkipConstructor = true)]
    internal class Message
    {
        [ProtoMember(1)]
        public long ContentLength { get; set; }

        [ProtoMember(2)]
        public byte[] PresharedKey { get; set; }

        [ProtoMember(3), DefaultValue(MessageType.Unknow)]
        public MessageType MessageType { get; set; }

        [ProtoMember(4)]
        public Header Header { get; set; }

        [ProtoMember(5)]
        public DateTime SenderTimestamp { get; set; } = DateTime.UtcNow;

        [ProtoMember(6)]
        public DateTime Expiration { get; set; }

        [ProtoMember(7)]
        public string ConversationGuid { get; set; }

        [ProtoIgnore]
        public Stream DataStream { get; set; }

        internal Message() { }
        internal Message(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new ArgumentException("Cannot read from stream.");
        }

        internal Message(
            Header headerPacket,
            long contentLength,
            Stream stream,
            MessageType type,
            DateTime expiration,
            string convGuid)
        {
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
            if (contentLength > 0)
            {
                if (stream == null || !stream.CanRead)
                {
                    throw new ArgumentException("Cannot read from supplied stream.");
                }
            }
            ContentLength = contentLength;
            Header = headerPacket;
            MessageType = type;
            Expiration = expiration;
            ConversationGuid = convGuid;
            if (type == MessageType.ResponsePack) SenderTimestamp = DateTime.UtcNow;
        }
    }
}