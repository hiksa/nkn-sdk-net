using System;

namespace NknSdk.Common.Exceptions
{
    public class InvalidResponseException : ApplicationException
    {
        private const string DefaultMessage = "invalid response from RPC server";

        public InvalidResponseException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
