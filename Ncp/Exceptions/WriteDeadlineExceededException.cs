using System;

namespace Ncp.Exceptions
{
    public class WriteDeadlineExceededException : ApplicationException
    {
        private const string DefaultMessage = "write deadline exceeded";

        public WriteDeadlineExceededException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
