using System;

namespace Synthesis.GuestService.Exceptions
{
    public class EmailRecentlySentException : Exception
    {
        public EmailRecentlySentException()
        {
        }

        public EmailRecentlySentException(string message) : base(message)
        {
        }

        public EmailRecentlySentException(string message, Exception ex) : base(message, ex)
        {
        }
    }
}
