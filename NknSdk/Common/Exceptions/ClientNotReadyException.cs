using System;

namespace NknSdk.Common.Exceptions
{
    public class ClientNotReadyException : ApplicationException
    {
        private const string DefaultMessage = "client not ready";

        public ClientNotReadyException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
