using System;

namespace Ncp.Exceptions
{
    public class ReadDeadlineExceededException : ApplicationException
    {
        private const string DefaultMessage = "read deadline exceeded";

        public ReadDeadlineExceededException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
