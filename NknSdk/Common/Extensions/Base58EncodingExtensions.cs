using System;
using System.Text;

namespace NknSdk.Common.Extensions
{
    public static class Base58EncodingExtensions
    {
        private static readonly int[] base58Indexes = new int[128];
        private static readonly char[] base58Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz".ToCharArray();

        static Base58EncodingExtensions()
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

        public static string Base58Encode(this byte[] input)
        {
            if (input.Length == 0)
            {
                return string.Empty;
            }

            input = CopyOfRange(input, 0, input.Length);

            var zeroCount = 0;
            while (zeroCount < input.Length && input[zeroCount] == 0)
            {
                zeroCount++;
            }

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

            while (j < temp.Length && temp[j] == base58Alphabet[0])
            {
                ++j;
            }

            while (--zeroCount >= 0)
            {
                temp[--j] = (byte)base58Alphabet[0];
            }

            var output = CopyOfRange(temp, j, temp.Length);

            try
            {
                return Encoding.ASCII.GetString(output);
            }
            catch (DecoderFallbackException e)
            {
                Console.WriteLine(e.ToString());
                return string.Empty;
            }
        }

        public static byte[] Base58Decode(this string input)
        {
            if (input.Length == 0)
            {
                return new byte[0];
            }

            var input58 = new byte[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                var c = input[i];

                var digit58 = -1;
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

            var zeroCount = 0;
            while (zeroCount < input58.Length && input58[zeroCount] == 0)
            {
                zeroCount++;
            }

            var temp = new byte[input.Length];
            var j = temp.Length;

            var startAt = zeroCount;
            while (startAt < input58.Length)
            {
                var mod = DivMod256(input58, startAt);
                if (input58[startAt] == 0)
                {
                    ++startAt;
                }

                temp[--j] = mod;
            }

            while (j < temp.Length && temp[j] == 0)
            {
                j++;
            }

            return CopyOfRange(temp, j - zeroCount, temp.Length);
        }

        private static byte DivMod58(byte[] number, int startAt)
        {
            var remainder = 0;
            for (int i = startAt; i < number.Length; i++)
            {
                var digit256 = number[i] & 0xFF;
                var temp = remainder * 256 + digit256;

                number[i] = (byte)(temp / 58);

                remainder = temp % 58;
            }

            return (byte)remainder;
        }

        private static byte DivMod256(byte[] number58, int startAt)
        {
            var remainder = 0;
            for (int i = startAt; i < number58.Length; i++)
            {
                var digit58 = number58[i] & 0xFF;
                var temp = remainder * 58 + digit58;

                number58[i] = (byte)(temp / 256);

                remainder = temp % 256;
            }

            return (byte)remainder;
        }

        private static byte[] CopyOfRange(byte[] source, int from, int to)
        {
            var range = new byte[to - from];
            for (int i = 0; i < to - from; i++)
            {
                range[i] = source[from + i];
            }

            return range;
        }
    }
}
