using System;

namespace NknSdk.Common.Exceptions
{
    public class InvalidWalletVersionException : ApplicationException
    {
        private const string DefaultMessage = "invalid wallet version";

        public InvalidWalletVersionException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
