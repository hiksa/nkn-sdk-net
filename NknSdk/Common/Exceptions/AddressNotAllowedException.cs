using System;

namespace NknSdk.Common.Exceptions
{
    public class AddressNotAllowedException : ApplicationException
    {
        private const string DefaultMessage = "address not allowed";

        public AddressNotAllowedException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
