using System;

namespace NknSdk.Common.Exceptions
{
    public class DataSizeTooLargeException : ApplicationException
    {
        private const string DefaultMessage = "data size too large";

        public DataSizeTooLargeException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
