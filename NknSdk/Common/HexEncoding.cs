
using System;

namespace NknSdk.Common
{
    public static class HexEncoding
    {
        public static string EncodeHex(this byte value) => new byte[] { value }.ToHexString();

        public static string EncodeHex(this short value) => BitConverter.GetBytes(value).ToHexString();

        public static string EncodeHex(this ushort value) => BitConverter.GetBytes(value).ToHexString();

        public static string EncodeHex(this int value) => BitConverter.GetBytes(value).ToHexString();

        public static string EncodeHex(this uint value) => BitConverter.GetBytes(value).ToHexString();

        public static string EncodeHex(this long value) => BitConverter.GetBytes(value).ToHexString();

        public static string EncodeHex(this ulong value) => BitConverter.GetBytes(value).ToHexString();

        public static string EncodeHex(this byte[] value)
        {
            if (value == null)
            {
                return "00";
            }

            var firstPart = EncodeInteger(value.Length);
            var secondPart = value.ToHexString();

            return firstPart + secondPart;
        }

        public static string EncodeHex(this bool value)
            => (value == true ? (byte)1 : (byte)0).EncodeHex();

        private static string EncodeInteger(int value)
        {
            if (value <= byte.MaxValue)
            {
                return ((byte)value).EncodeHex();
            }
            else if (value <= ushort.MaxValue)
            {
                return ((ushort)value).EncodeHex();
            }
            else if (value <= uint.MaxValue)
            {
                return ((uint)value).EncodeHex();
            }

            throw new Exception();
        }
    }
}
