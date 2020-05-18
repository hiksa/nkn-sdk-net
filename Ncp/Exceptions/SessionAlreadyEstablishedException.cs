using System;

namespace Ncp.Exceptions
{
    public class SessionAlreadyEstablishedException : ApplicationException
    {
        private const string DefaultMessage = "session is already established";

        public SessionAlreadyEstablishedException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
