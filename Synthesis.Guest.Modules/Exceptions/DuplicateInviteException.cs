using System;

namespace Synthesis.GuestService.Exceptions
{
    public class DuplicateInviteException : Exception
    {
        public DuplicateInviteException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public DuplicateInviteException(string message) : base(message)
        {
        }

        public DuplicateInviteException()
        {
        }
    }
}
