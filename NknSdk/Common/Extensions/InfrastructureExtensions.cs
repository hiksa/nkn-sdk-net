using System;
using System.Linq;
using System.IO;

using zlib;

using NknSdk.Common.Exceptions;

namespace NknSdk.Common.Extensions
{
    public static class InfrastructureExtensions
    {
        public static string ToHexString(this byte[] bytes)
            => BitConverter
                .ToString(bytes)
                .Replace("-", "")
                .ToLower();
        
        public static byte[] FromHexString(this string hex)
            => Enumerable
                .Range(0, hex.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                .ToArray();
        
        public static byte[] Compress(this byte[] data)
        {
            using (var outMemoryStream = new MemoryStream())
            using (var outZStream = new ZOutputStream(outMemoryStream, zlibConst.Z_DEFAULT_COMPRESSION))
            using (var inMemoryStream = new MemoryStream(data))
            {
                CopyStream(inMemoryStream, outZStream);

                outZStream.finish();

                return outMemoryStream.ToArray();
            }
        }

        public static byte[] Decompress(this byte[] gzip)
        {
            using (var outMemoryStream = new MemoryStream())
            using (var outZStream = new ZOutputStream(outMemoryStream))
            using (var inMemoryStream = new MemoryStream(gzip))
            {
                CopyStream(inMemoryStream, outZStream);

                outZStream.finish();

                return outMemoryStream.ToArray();
            }
        }

        public static T[] Concat<T>(this T[] first, T[] second)
            => Enumerable.Concat(first, second).ToArray();

        public static void ThrowIfNullOrEmpty(this string instance, string message = null)
        {
            if (string.IsNullOrEmpty(instance))
            {
                throw new InvalidArgumentException(message);
            }
        }

        private static void CopyStream(Stream input, Stream output)
        {
            var buffer = new byte[2000];
            int len;
            while ((len = input.Read(buffer, 0, 2000)) > 0)
            {
                output.Write(buffer, 0, len);
            }

            output.Flush();
        }
    }
}
