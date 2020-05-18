using System;

namespace NknSdk.Common.Exceptions
{
    public class ServerException : ApplicationException
    {
        private const string DefaultMessage = "error from RPC server";

        public ServerException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
