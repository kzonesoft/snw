using System;
namespace Kzone.Signal
{
    public static class HeaderEx
    {

        public static Header BuildTag<TEnum>(TEnum dataTag) where TEnum : struct
        {
            return new Header()
            {
                {
                    _tag,
                    dataTag.ToString()
                }
            };
        }


        public static Header BuildResponse(ResponseStatusCode replyStatus)
        {
            return new Header()
            {
                {
                    _statusCode,
                    replyStatus.ToString()
                }
            };
        }

        public static Header Build<TEnum>(TEnum dataTag, ResponseStatusCode replyStatus) where TEnum : struct
        {
            return new Header()
            {
                {
                    _tag,
                    dataTag.ToString()
                },
                {
                    _statusCode,
                    replyStatus.ToString()
                }
            };
        }
        public static string GetTag(this Header header)
        {
            return header.TryGetValue(_tag, out string enumName)
                ? enumName
                : default;
        }

        public static TEnum GetTag<TEnum>(this Header header) where TEnum : struct
        {
            return header.TryGetValue(_tag, out string enumName)
                ? CastToEnum<TEnum>(enumName)
                : default;
        }


        public static ResponseStatusCode GetStatusCode(this Header header)
        {
            return header.TryGetValue(_statusCode, out string enumName)
                ? CastToEnum<ResponseStatusCode>(enumName)
                : default;
        }


        internal static Header BuildChannel(int channel)
        {
            return new Header()
            {
                {
                    _channel,
                    channel.ToString()
                }
            };
        }
        internal static int GetChannel(this Header header)
        {
            return header.TryGetValue(_channel, out string channel)
                ? Convert.ToInt32(channel)
                : default;
        }
        private static TEnum CastToEnum<TEnum>(string value) where TEnum : struct
        {
            if (string.IsNullOrWhiteSpace(value))
                return default;
            return Enum.TryParse(value, true, out TEnum result) ? result : default;
        }

        private readonly static string _tag = "746167";
        private readonly static string _statusCode = "72636f6465";
        private readonly static string _channel = "2f94027";
    }
}
