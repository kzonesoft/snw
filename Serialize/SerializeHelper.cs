using Kzone.Signal.Common;
using ProtoBuf;
using System;
using System.IO;
using System.Security.Cryptography;


namespace Kzone.Signal
{
    internal static class SerializeHelper
    {
        private static RandomNumberGenerator _randomNumber = RandomNumberGenerator.Create();
        public static byte[] Serialize(this object obj)
        {
            if (obj == null)
                throw new NullReferenceException(string.Format("Object is null"));
            var objType = obj.GetType();
            if (objType == typeof(byte[]))
            {
                return (byte[])obj;
            }
            else if (!ProtoBuf.Meta.RuntimeTypeModel.Default.CanSerialize(objType))
            {
                throw new NotSupportedException(string.Format("This {0} not support", nameof(objType)));
            }
            using MemoryStream stream = new();
            Serializer.Serialize(stream, obj);
            return stream.ToArray();
        }

        public static void SerializeToStream(this object obj, Stream stream)
        {
            if (obj == null)
                throw new NullReferenceException("Object is null");

            var objType = obj.GetType();
            if (objType == typeof(byte[]))
            {
                var data = (byte[])obj;
                stream.Write(data, 0, data.Length);
            }
            else if (!ProtoBuf.Meta.RuntimeTypeModel.Default.CanSerialize(objType))
            {
                throw new NotSupportedException(string.Format("This {0} not support", nameof(objType)));
            }
            else
            {
                Serializer.Serialize(stream, obj);
            }
        }

        public static T Deserialize<T>(this byte[] data)
        {
            if (data == null)
                throw new NullReferenceException(string.Format("{0} is null", nameof(T)));
            if (typeof(T) == typeof(byte[]))
            {
                return (T)Convert.ChangeType(data, typeof(byte[]));
            }
            else if (!ProtoBuf.Meta.RuntimeTypeModel.Default.CanSerialize(typeof(T)))
            {
                throw new NotSupportedException(string.Format("This {0} not support", nameof(T)));
            }
            using MemoryStream stream = new(data);
            return Serializer.Deserialize<T>(stream);
        }

        public static byte[] SerializeWithAes(this object obj, byte[] key)
        {
            if (obj == null)
                throw new NullReferenceException(string.Format("Object is null"));
            var objType = obj.GetType();
            if (objType == typeof(byte[]))
            {
                return (byte[])obj;
            }
            else if (!ProtoBuf.Meta.RuntimeTypeModel.Default.CanSerialize(objType))
            {
                throw new NotSupportedException(string.Format("This {0} not support", nameof(objType)));
            }
            using MemoryStream stream = new();
            byte[] iv = new byte[16];
            _randomNumber.GetBytes(iv);
            stream.Write(iv, 0, 16);

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            try
            {
                using CryptoStream cryptoStream = new(stream, aes.CreateEncryptor(), CryptoStreamMode.Write);
                Serializer.Serialize(cryptoStream, obj);
                cryptoStream.FlushFinalBlock();
                return stream.ToArray();
            }
            catch
            {
                throw new NotSupportedException(string.Format("This {0} not support", nameof(objType)));
            }
        }

        public static T DeserializeAes<T>(this byte[] data, byte[] key)
        {
            if (data == null)
                throw new NullReferenceException(string.Format("{0} is null", nameof(T)));
            if (typeof(T) == typeof(byte[]))
            {
                return (T)Convert.ChangeType(data, typeof(byte[]));
            }
            else if (!ProtoBuf.Meta.RuntimeTypeModel.Default.CanSerialize(typeof(T)))
            {
                throw new NotSupportedException(string.Format("This {0} not support", nameof(T)));
            }
            using MemoryStream stream = new(data);
            try
            {
                byte[] iv = new byte[16];
                stream.Read(iv, 0, 16);

                using var aes = Aes.Create();
                aes.Key = key;
                aes.IV = iv;

                using CryptoStream cryptoStream = new(stream, aes.CreateDecryptor(), CryptoStreamMode.Read);
                return Serializer.Deserialize<T>(cryptoStream);
            }
            catch
            {
                throw new NotSupportedException(string.Format("This {0} not support", typeof(T)));
            }
        }

        internal static byte[] ToHeaderBytes(this Message mes, byte[] headerKey)
        {
            using (var stream = new MemoryStream())
            {
                using (var cryptoStream = new CryptoStream(stream, new XorCryptoTransform(headerKey), CryptoStreamMode.Write))
                {
                    Serializer.Serialize(cryptoStream, mes);
                    cryptoStream.FlushFinalBlock();
                }
                return stream.ToArray();
            }
        }

        internal static Message ToMessage(this byte[] bytes, byte[] headerKey)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (headerKey == null || headerKey.Length == 0) throw new ArgumentNullException(nameof(headerKey));
            if (bytes.Length <= 4) throw new ArgumentException("Byte array is too small to process.");
            using MemoryStream stream = new(bytes, 0, bytes.Length - 4);
            using CryptoStream cryptoStream = new(stream, new XorCryptoTransform(headerKey), CryptoStreamMode.Read);
            return Serializer.Deserialize<Message>(cryptoStream);
        }
    }
}
