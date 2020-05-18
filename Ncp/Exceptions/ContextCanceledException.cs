using System;

namespace Ncp.Exceptions
{
    public class ContextCanceledException : ApplicationException
    {
        private const string DefaultMessage = "context canceled";

        public ContextCanceledException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
