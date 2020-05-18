using System;

namespace NknSdk.Common.Exceptions
{
    public class InvalidWalletFormatException : ApplicationException
    {
        private const string DefaultMessage = "invalid wallet format";

        public InvalidWalletFormatException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
