using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NknSdk.MultiClient
{
    public static class Utils
    {
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

        public static string AddIdentifier(string address, string identifier)
        {
            if (identifier == "")
            {
                return address;
            }

            return AddIdentifierPrefix(address, "__" + identifier + "__");
        }

        public static (string Address, string ClientId) RemoveIdentifier(string source)
        {
            var parts = source.Split('.');
            if (MultiClientConstants.MultiClientIdentifierRegex.IsMatch(parts[0]))
            {
                var address = string.Join(".", parts.Skip(1));
                return (address, parts[0]);
            }

            return (source, "");
        }

        public static string MakeSessionKey(string remoteAddress, string sessionId)
            => remoteAddress + sessionId;
    }
}
