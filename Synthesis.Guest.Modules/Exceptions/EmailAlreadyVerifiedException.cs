using System;

namespace Synthesis.GuestService.Exceptions
{
    public class EmailAlreadyVerifiedException : Exception
    {
        public EmailAlreadyVerifiedException()
        {
        }

        public EmailAlreadyVerifiedException(string message) : base(message)
        {
        }

        public EmailAlreadyVerifiedException(string message, Exception ex) : base(message, ex)
        {
        }
    }
}
