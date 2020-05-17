using System;
using System.Collections.Generic;
using System.Text;

namespace NknSdk.Common.Protobuf.Messages
{
    public interface ISerializable
    {
    }

    public static class ISerializableExtensions
    {
        public static T BytesTo<T>(this byte[] data) where T : class, ISerializable
        {
            var result = ProtoSerializer.Deserialize<T>(data);

            return result;
        }

        public static byte[] ToBytes<T>(this T instance) where T : class, ISerializable
        {
            var result = ProtoSerializer.Serialize(instance);

            return result;
        }
    }
}
