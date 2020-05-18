using System;

namespace Ncp.Exceptions
{
    public class MissingHandshakeException : ApplicationException
    {
        private const string DefaultMessage = "first packet is not handshake packet";

        public MissingHandshakeException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
