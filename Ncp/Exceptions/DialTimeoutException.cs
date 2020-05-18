using System;

namespace Ncp.Exceptions
{
    public class DialTimeoutException : ApplicationException
    {
        private const string DefaultMessage = "dial timeout";

        public DialTimeoutException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
