using System;

namespace Ncp.Exceptions
{
    public class BufferSizeTooSmallException : ApplicationException
    {
        private const string DefaultMessage = "read buffer size is less than data length in non-session mode";

        public BufferSizeTooSmallException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
