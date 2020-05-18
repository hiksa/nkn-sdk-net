using System;

namespace NknSdk.Common.Exceptions
{
    public class InvalidArgumentException : ApplicationException
    {
        private const string DefaultMessage = "invalid argument";

        public InvalidArgumentException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
