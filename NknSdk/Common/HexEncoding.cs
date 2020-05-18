
namespace NknSdk.Common
{
    public static class HexEncoding
    {
        public static string EncodeHex(this short value) => value.ToString("X").PadLeft(2, '0');

        public static string EncodeHex(this int value) => value.ToString("X").PadLeft(2, '0');

        public static string EncodeHex(this uint value) => value.ToString("X").PadLeft(2, '0');

        public static string EncodeHex(this long value) => value.ToString("X").PadLeft(2, '0');

        public static string EncodeHex(this ulong value) => value.ToString("X").PadLeft(2, '0');

        public static string EncodeHex(this byte[] value)
        {
            var firstPart = value == null ? "0" : value.Length.EncodeHex();
            var secondPart = value == null ? "0" : value.ToHexString();
            return firstPart + secondPart;
        }

        public static string EncodeHex(this bool value)
            => (value == true ? 1 : 0).EncodeHex();
    }
}
