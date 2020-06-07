using System;
using System.Text;
using System.Security.Cryptography;
using System.Linq;
using System.IO;
using System.IO.Compression;
using NknSdk.Common.Exceptions;
using zlib;

namespace NknSdk.Common
{
    public static class Extensions
    {
        private static readonly int[] base58Indexes = new int[128];
        private static readonly char[] base58Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz".ToCharArray();

        static Extensions()
        {
            for (int i = 0; i < base58Indexes.Length; i++)
            {
                base58Indexes[i] = -1;
            }

            for (int i = 0; i < base58Alphabet.Length; i++)
            {
                base58Indexes[base58Alphabet[i]] = i;
            }
        }

        public static string ToHexString(this byte[] ba)
            => BitConverter.ToString(ba).Replace("-", "").ToLower();
        
        public static byte[] FromHexString(this string hex)
            => Enumerable
                .Range(0, hex.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                .ToArray();
        
        public static byte[] Compress(this byte[] data)
        {
            using (MemoryStream outMemoryStream = new MemoryStream())
            using (ZOutputStream outZStream = new ZOutputStream(outMemoryStream, zlibConst.Z_DEFAULT_COMPRESSION))
            using (Stream inMemoryStream = new MemoryStream(data))
            {
                CopyStream(inMemoryStream, outZStream);
                outZStream.finish();
                return outMemoryStream.ToArray();
            }
        }

        public static byte[] Decompress(this byte[] gzip)
        {
            using (MemoryStream outMemoryStream = new MemoryStream())
            using (ZOutputStream outZStream = new ZOutputStream(outMemoryStream))
            using (Stream inMemoryStream = new MemoryStream(gzip))
            {
                CopyStream(inMemoryStream, outZStream);
                outZStream.finish();
                return outMemoryStream.ToArray();
            }
        }

        private static void CopyStream(Stream input, Stream output)
        {
            byte[] buffer = new byte[2000];
            int len;
            while ((len = input.Read(buffer, 0, 2000)) > 0)
            {
                output.Write(buffer, 0, len);
            }

            output.Flush();
        }

        public static string Base58Encode(this byte[] input)
        {
            if (input.Length == 0)
            {
                return string.Empty;
            }

            input = CopyOfRange(input, 0, input.Length);

            // Count leading zeroes.
            int zeroCount = 0;
            while (zeroCount < input.Length && input[zeroCount] == 0)
            {
                zeroCount++;
            }

            // The actual encoding.
            var temp = new byte[input.Length * 2];
            var j = temp.Length;

            var startAt = zeroCount;
            while (startAt < input.Length)
            {
                var mod = DivMod58(input, startAt);
                if (input[startAt] == 0)
                {
                    startAt++;
                }

                temp[--j] = (byte)base58Alphabet[mod];
            }

            // Strip extra '1' if there are some after decoding.
            while (j < temp.Length && temp[j] == base58Alphabet[0])
            {
                ++j;
            }

            // Add as many leading '1' as there were leading zeros.
            while (--zeroCount >= 0)
            {
                temp[--j] = (byte)base58Alphabet[0];
            }

            var output = CopyOfRange(temp, j, temp.Length);

            try
            {
                return System.Text.Encoding.ASCII.GetString(output);
            }
            catch (DecoderFallbackException e)
            {
                Console.WriteLine(e.ToString());
                return string.Empty;
            }
        }

        public static byte[] Base58Decode(this string input)
        {
            if (0 == input.Length)
            {
                return new byte[0];
            }

            var input58 = new byte[input.Length];
            // Transform the String to a base58 byte sequence
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                int digit58 = -1;
                if (c >= 0 && c < 128)
                {
                    digit58 = base58Indexes[c];
                }

                if (digit58 < 0)
                {
                    throw new ArgumentException("Illegal character " + c + " at " + i);
                }

                input58[i] = (byte)digit58;
            }

            // Count leading zeroes
            int zeroCount = 0;
            while (zeroCount < input58.Length && input58[zeroCount] == 0)
            {
                zeroCount++;
            }

            // The encoding
            var temp = new byte[input.Length];
            var j = temp.Length;

            var startAt = zeroCount;
            while (startAt < input58.Length)
            {
                byte mod = DivMod256(input58, startAt);
                if (input58[startAt] == 0)
                {
                    ++startAt;
                }
                temp[--j] = mod;
            }

            // Do no add extra leading zeroes, move j to first non null byte.
            while (j < temp.Length && temp[j] == 0)
            {
                j++;
            }

            return CopyOfRange(temp, j - zeroCount, temp.Length);
        }

        public static T[] Concat<T>(this T[] first, T[] second)
            => Enumerable.Concat(first, second).ToArray();

        public static void ThrowIfNull<T>(this T instance, string message = null) where T : class
        {
            if (instance == null)
            {
                throw new InvalidArgumentException(message);
            }
        }

        public static void ThrowIfNullOrEmpty(this string instance, string message = null)
        {
            if (string.IsNullOrEmpty(instance))
            {
                throw new InvalidArgumentException(message);
            }
        }

        private static byte DivMod58(byte[] number, int startAt)
        {
            int remainder = 0;
            for (int i = startAt; i < number.Length; i++)
            {
                int digit256 = number[i] & 0xFF;
                int temp = remainder * 256 + digit256;

                number[i] = (byte)(temp / 58);

                remainder = temp % 58;
            }

            return (byte)remainder;
        }

        private static byte DivMod256(byte[] number58, int startAt)
        {
            int remainder = 0;
            for (int i = startAt; i < number58.Length; i++)
            {
                int digit58 = number58[i] & 0xFF;
                int temp = remainder * 58 + digit58;

                number58[i] = (byte)(temp / 256);

                remainder = temp % 256;
            }

            return (byte)remainder;
        }

        private static byte[] CopyOfRange(byte[] source, int from, int to)
        {
            byte[] range = new byte[to - from];
            for (int i = 0; i < to - from; i++)
            {
                range[i] = source[from + i];
            }

            return range;
        }
    }
}
