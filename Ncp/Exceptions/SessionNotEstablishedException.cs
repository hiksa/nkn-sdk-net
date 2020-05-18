using System;

namespace Ncp.Exceptions
{
    public class SessionNotEstablishedException : ApplicationException
    {
        private const string DefaultMessage = "session not established yet";

        public SessionNotEstablishedException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
