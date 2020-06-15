namespace NknSdk.Common.Protobuf
{
    public interface ISerializable
    {
    }

    public static class ISerializableExtensions
    {
        public static T FromBytes<T>(this byte[] data) where T : class, ISerializable
        {
            return ProtoSerializer.Deserialize<T>(data);
        }

        public static byte[] ToBytes<T>(this T instance) where T : class, ISerializable
        {
            return ProtoSerializer.Serialize(instance);
        }
    }
}
