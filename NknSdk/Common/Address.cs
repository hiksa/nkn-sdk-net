using System;
using System.Linq;

using NknSdk.Client;
using NknSdk.Common.Extensions;

namespace NknSdk.Common
{
    public static class Address
    {
        public const int Uint160Length = 20;
        public const int CheckSumLength = 4;
        public const string Prefix = "02b825";

        public static readonly int PrefixLength = Address.Prefix.Length / 2;
        public static readonly int FullLength = Address.PrefixLength + Address.Uint160Length + Address.CheckSumLength;

        public static bool Verify(string address)
        {
            try
            {
                var addressBytes = address.Base58Decode();
                if (addressBytes.Length != Address.FullLength)
                {
                    return false;
                }

                var addressPrefixBytes = new ArraySegment<byte>(addressBytes, 0, Address.PrefixLength).ToArray();
                var addressPrefix = addressPrefixBytes.ToHexString();
                if (addressPrefix != Address.Prefix)
                {
                    return false;
                }

                var programHash = Address.ToProgramHash(address);
                var addressVerificationCode = Address.GetVerificationCodeFromAddress(address);
                var programHashVerificationCode = Address.GetVerificationCodeFromProgramHash(programHash);

                return addressVerificationCode == programHashVerificationCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string PublicKeyToSignatureRedeem(string publicKey) => $"20{publicKey}ac";

        public static string HexStringToProgramHash(string hexString)
        {
            var sha256hash = Hash.Sha256Hex(hexString);

            var result = Hash.Ripemd160Hex(sha256hash);

            return result.ToHexString();
        }

        public static string SignatureToParameter(string hex) => "40" + hex;

        public static string PrefixByteCountToHexString(string hex)
        {
            var length = hex.Length;
            if (length == 0)
            {
                return "00";
            }

            if (length % 2 == 1)
            {
                hex = "0" + hex;
                length++;
            }

            var byteCount = length / 2;
            var byteCountHex = byteCount.ToString("x");
            if (byteCountHex.Length % 2 == 1)
            {
                byteCountHex = "0" + byteCountHex;
            }

            return byteCountHex + hex;
        }

        public static string ToProgramHash(string address)
        {
            var addressBytes = address.Base58Decode();
            
            var bytesToTake = addressBytes.Length - Address.PrefixLength - Address.CheckSumLength;
            
            var programHashBytes = new ArraySegment<byte>(addressBytes, Address.PrefixLength, bytesToTake).ToArray();
            
            return programHashBytes.ToHexString();
        }

        public static string FromProgramHash(string programHash)
        {
            var addressVerifyBytes = Address.GetVerificationBytesFromProgramHash(programHash);

            var prefixedProgramHash = Address.Prefix + programHash;

            var addressBaseData = prefixedProgramHash.FromHexString();

            var result = addressBaseData.Concat(addressVerifyBytes);

            return result.Base58Encode();
        }

        public static string AddressToId(string address) => Hash.Sha256(address);

        public static string AddressToPublicKey(string address)
            => address
                .Split(new char[] { '.' })
                .LastOrDefault();

        public static string AddIdentifier(string address, string identifier)
        {
            if (identifier == "")
            {
                return address;
            }

            return Address.AddIdentifierPrefix(address, $"__{identifier}__");
        }

        public static string AddIdentifierPrefix(string identifier, string prefix)
        {
            if (identifier == "")
            {
                return "" + prefix;
            }

            if (prefix == "")
            {
                return "" + identifier;
            }

            return prefix + "." + identifier;
        }

        public static (string Address, string ClientId) RemoveIdentifier(string source)
        {
            var parts = source.Split('.');

            if (Constants.MultiClientIdentifierRegex.IsMatch(parts[0]))
            {
                var address = string.Join(".", parts.Skip(1));
                return (address, parts[0]);
            }

            return (source, "");
        }

        private static string GetVerificationCodeFromAddress(string address)
        {
            var addressBytes = address.Base58Decode();

            var offset = addressBytes.Length - Address.CheckSumLength;

            var verificationBytes = new ArraySegment<byte>(addressBytes, offset, Address.CheckSumLength).ToArray();
            
            return verificationBytes.ToHexString();
        }

        private static byte[] GetVerificationBytesFromProgramHash(string programHash)
        {
            var prefixedProgramHash = Address.Prefix + programHash;

            var prefixedProgramHashBytes = prefixedProgramHash.FromHexString();

            var programHashHex = Hash.DoubleSha256(prefixedProgramHashBytes);

            var verificationBytes = programHashHex.FromHexString();

            var addressVerificationBytes = verificationBytes.Take(Address.CheckSumLength).ToArray();

            return addressVerificationBytes;
        }

        private static string GetVerificationCodeFromProgramHash(string programHash)
        {
            var verificationBytes = Address.GetVerificationBytesFromProgramHash(programHash);
            
            return verificationBytes.ToHexString();
        }
    }
}
