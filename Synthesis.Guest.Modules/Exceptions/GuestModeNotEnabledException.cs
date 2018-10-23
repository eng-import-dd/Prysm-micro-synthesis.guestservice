using System;

namespace Synthesis.GuestService.Exceptions
{
    public class GuestModeNotEnabledException : Exception
    {
        public GuestModeNotEnabledException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public GuestModeNotEnabledException(string message) : base(message)
        {
        }

        public GuestModeNotEnabledException()
        {
        }
    }
}
