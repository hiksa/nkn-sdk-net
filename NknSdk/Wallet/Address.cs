using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualBasic;
using NknSdk.Common;

namespace NknSdk.Wallet
{
    public static class Address
    {
        public static bool IsCorrectAddress(string address)
        {
            try
            {
                var addressBytes = address.Base58Decode();
                if (addressBytes.Length != Constants.AddressLength)
                {
                    return false;
                }

                var addressPrefixBytes = new ArraySegment<byte>(addressBytes, 0, Constants.AddressPrefixLength);
                var addressPrefix = addressPrefixBytes.ToArray().ToHexString();
                if (addressPrefix != Constants.AddressPrefix)
                {
                    return false;
                }

                var programHash = AddressStringToProgramHash(address);
                var addressVerificationCode = GetAddressVerificationCode(address);
                var programHashVerificationCode = GenerateAddressVerificationCodeFromProgramHash(programHash);

                return addressVerificationCode == programHashVerificationCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string PublicKeyToSignatureRedeem(string publicKey)
            => $"20{publicKey}ac";

        public static string HexStringToProgramHash(string hexString)
        {
            var sha256hash = Crypto.Sha256Hex(hexString);
            //var result = Utils.

            return sha256hash;
        }

        public static string ProgramHashStringToAddress(string programHash)
        {
            var addressVerifyBytes = GenerateAddressVerificationBytesFromProgramHash(programHash);
            
            var addressBaseData = (Constants.AddressPrefix + programHash).FromHexString();
            
            var result = addressBaseData.Concat(addressVerifyBytes);
            
            return result.Base58Encode();
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

        private static string AddressStringToProgramHash(string address)
        {
            var addressBytes = address.Base58Decode();
            
            var bytesToTake = addressBytes.Length - Constants.AddressPrefixLength - Constants.CheckSumLength;
            
            var programHashBytes = new ArraySegment<byte>(addressBytes, Constants.AddressPrefixLength, bytesToTake);
            
            return programHashBytes.ToArray().ToHexString();
        }

        private static string GetAddressVerificationCode(string address)
        {
            var addressBytes = address.Base58Decode();

            var verificationBytes = new ArraySegment<byte>(addressBytes, addressBytes.Length - Constants.CheckSumLength, Constants.CheckSumLength);
            
            return verificationBytes.ToArray().ToHexString();
        }

        private static byte[] GenerateAddressVerificationBytesFromProgramHash(string programHash)
        {
            var prefixedProgramHash = Constants.AddressPrefix + programHash;

            var prefixedProgramHashBytes = prefixedProgramHash.FromHexString();

            var programHashHex = Crypto.DoubleSha256(prefixedProgramHashBytes);

            var verificationBytes = programHashHex.FromHexString();

            var addressVerificationBytes = verificationBytes.Take(Constants.CheckSumLength).ToArray();

            return addressVerificationBytes;
        }

        private static string GenerateAddressVerificationCodeFromProgramHash(string programHash)
        {
            var verificationBytes = GenerateAddressVerificationBytesFromProgramHash(programHash);
            
            return verificationBytes.ToHexString();
        }
    }
}
