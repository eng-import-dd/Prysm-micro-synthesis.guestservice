using System;

namespace Synthesis.GuestService.Exceptions
{
    public class GetProjectException : Exception
    {
        public GetProjectException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public GetProjectException(string message) : base(message)
        {
        }

        public GetProjectException()
        {
        }
    }
}
