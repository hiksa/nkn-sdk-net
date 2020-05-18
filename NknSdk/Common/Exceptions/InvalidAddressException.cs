using System;

namespace NknSdk.Common.Exceptions
{
    public class InvalidAddressException : ApplicationException
    {
        private const string DefaultMessage = "invalid wallet address";

        public InvalidAddressException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
