using System;

namespace Ncp.Exceptions
{
    public class InvalidPacketException : ApplicationException
    {
        private const string DefaultMessage = "invalid packet";

        public InvalidPacketException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
