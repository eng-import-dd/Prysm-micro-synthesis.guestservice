using System;

namespace Synthesis.GuestService.Exceptions
{
    public class ResetAccessCodeException : Exception
    {
        public ResetAccessCodeException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public ResetAccessCodeException(string message) : base(message)
        {
        }

        public ResetAccessCodeException()
        {
        }
    }
}
