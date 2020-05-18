using System;

namespace NknSdk.Common.Exceptions
{
    public class DecryptionException : ApplicationException
    {
        private const string DefaultMessage = "decrypt message error";

        public DecryptionException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
