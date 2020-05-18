using System;

namespace Ncp.Exceptions
{
    public class SessionClosedException : ApplicationException
    {
        private const string DefaultMessage = "session closed";

        public SessionClosedException(string message = DefaultMessage)
            :base(message)
        {
        }
    }
}
