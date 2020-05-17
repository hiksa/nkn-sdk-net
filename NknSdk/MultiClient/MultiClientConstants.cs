using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace NknSdk.MultiClient
{
    public static class MultiClientConstants
    {
        public const int AcceptSessionBufferSize = 128;

        public const int SessionIdSize = 8;

        public static Regex DefaultSessionAllowedAddressRegex = new Regex("/.*/");

        public static Regex MultiClientIdentifierRegex = new Regex(@"^__\d+__$");
    }
}
