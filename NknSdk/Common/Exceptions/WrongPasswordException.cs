using System;

namespace NknSdk.Common.Exceptions
{
    public class WrongPasswordException : ApplicationException
    {
        private const string DefaultMessage = "wrong password";

        public WrongPasswordException(string message = DefaultMessage)
            : base(message)
        {
        }
    }
}
