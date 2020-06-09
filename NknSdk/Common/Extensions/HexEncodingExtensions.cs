using System;

using NknSdk.Common.Protobuf.SignatureChain;
using NknSdk.Common.Protobuf.Transaction;

namespace NknSdk.Common.Extensions
{
    public static class HexEncodingExtensions
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

            var firstPart = value.Length.EncodeHexVariableLength();
            var secondPart = value.ToHexString();

            return firstPart + secondPart;
        }

        public static string EncodeHex(this bool value)
            => (value == true ? (byte)1 : (byte)0).EncodeHex();

        public static string EncodeHex(this Payload payload)
        {
            var result = ((int)payload.Type).EncodeHex();
            result += payload.Data.EncodeHex();

            return result;
        }

        public static string EncodeHex(this UnsignedTransaction unsigned)
        {
            var hex = unsigned.Payload.EncodeHex();
            hex += unsigned.Nonce.EncodeHex();
            hex += unsigned.Fee.EncodeHex();
            hex += unsigned.Attributes.EncodeHex();

            return hex;
        }

        public static string EncodeHex(this SignatureChain signatureChain)
        {
            var hex = signatureChain.Nonce.EncodeHex();
            hex += signatureChain.DataSize.EncodeHex();
            hex += signatureChain.BlockHash.EncodeHex();
            hex += signatureChain.SourceId.EncodeHex();
            hex += signatureChain.SourcePublicKey.EncodeHex();
            hex += signatureChain.DestinationId.EncodeHex();
            hex += signatureChain.DestinationPublicKey.EncodeHex();

            return hex;
        }

        public static string EncodeHex(this SignatureChainElement element)
        {
            var hex = element.Id.EncodeHex();
            hex += element.NextPublicKey.EncodeHex();
            hex += element.IsMining.EncodeHex();

            return hex;
        }

        private static string EncodeHexVariableLength(this int value)
        {
            if (value <= byte.MaxValue)
            {
                return ((byte)value).EncodeHex();
            }
            else if (value <= ushort.MaxValue)
            {
                return ((ushort)value).EncodeHex();
            }
            else if (value <= int.MaxValue)
            {
                return ((uint)value).EncodeHex();
            }

            throw new Exception();
        }
    }
}
