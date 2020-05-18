using System;

namespace Ncp.Exceptions
{
    public class DataSizeTooLargeException : ApplicationException
    {
        private const string DefaultMessage = "data size is greater than session mtu in non-session mode";

        public DataSizeTooLargeException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
