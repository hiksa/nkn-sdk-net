using System;

namespace NknSdk.Common.Exceptions
{
    public class InvalidDestinationException : ApplicationException
    {
        private const string DefaultMessage = "invalid destination";

        public InvalidDestinationException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
