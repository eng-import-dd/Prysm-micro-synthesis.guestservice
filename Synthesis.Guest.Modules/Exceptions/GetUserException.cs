using System;

namespace Synthesis.GuestService.Exceptions
{
    public class GetUserException : Exception
    {
        public GetUserException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public GetUserException(string message) : base(message)
        {
        }

        public GetUserException()
        {
        }
    }
}
