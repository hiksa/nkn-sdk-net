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

            random.NextBytes(result);

            return result;
        }

        public static string RandomBytesAsHexString(int length)
            => RandomBytes(length).ToHexString();

        public static int RandomInt() => random.Next();
    }
}
