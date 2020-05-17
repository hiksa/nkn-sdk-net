using ProtoBuf;
using System.IO;

namespace NknSdk.Common.Protobuf
{
    public class ProtoSerializer
    {
        public static byte[] Serialize<T>(T record) where T : class
        {
            if (record == null)
            {
                return null;
            }

            using (var stream = new MemoryStream())
            {
                Serializer.Serialize(stream, record);

                return stream.ToArray();
            }
        }

        public static T Deserialize<T>(byte[] data) where T : class
        {
            if (null == data)
            {
                return null;
            }

            using (var stream = new MemoryStream(data))
            {
                var result = Serializer.Deserialize<T>(stream);

                return result;
            }
        }
    }
}
