using System;

namespace Ncp.Exceptions
{
    public class RecieveWindowFullException : ApplicationException
    {
        private const string DefaultMessage = "receive window full";

        public RecieveWindowFullException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
