using System;

namespace NknSdk.Common.Exceptions
{
    public class InsufficientBalanceException : ApplicationException
    {
        private const string DefaultMessage = "insufficient balance";

        public InsufficientBalanceException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
