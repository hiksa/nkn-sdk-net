using System;
using System.Collections.Generic;
using System.Text;

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
            var result = new byte[length];

            //Array.Fill<byte>(result, 1);
            random.NextBytes(result);

            return result;
        }

        public static string RandomBytesAsHexString(int length) => RandomBytes(length).ToHexString();

        //public static int RandomInt() => 1;
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
