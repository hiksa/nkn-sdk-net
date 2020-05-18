using System;

namespace NknSdk.Common.Exceptions
{
    public class UnknownException : ApplicationException
    {
        private const string DefaultMessage = "unknown error";

        public UnknownException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
