using System;

using NknSdk.Common.Extensions;

namespace NknSdk.Common
{
    public static class PseudoRandom
    {
        private static Random random;

        static PseudoRandom()
        {
            random = new Random();
        }

        public static byte[] RandomBytes(int length)
        {
            var buffer = new byte[length];

            random.NextBytes(buffer);

            return buffer;
        }

        public static string RandomBytesAsHexString(int length) => PseudoRandom.RandomBytes(length).ToHexString();

        public static int RandomInt(int min = 0, int max = int.MaxValue) => random.Next(min, max);

        public static long RandomLong(long min = 0, long max = long.MaxValue)
        {
            var buffer = new byte[8];

            random.NextBytes(buffer);

            var result = BitConverter.ToInt64(buffer, 0);

            return Math.Abs(result % (max - min)) + min;
        }
    }
}
