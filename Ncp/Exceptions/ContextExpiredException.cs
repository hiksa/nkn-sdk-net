using System;

namespace Ncp.Exceptions
{
    public class ContextExpiredException : ApplicationException
    {
        private const string DefaultMessage = "context expired";

        public ContextExpiredException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
