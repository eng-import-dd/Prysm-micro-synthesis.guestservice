using System;

namespace Synthesis.GuestService.Exceptions
{
    public class BuildEmailException : Exception
    {
        public BuildEmailException()
        {
        }

        public BuildEmailException(string message) : base(message)
        {
        }

        public BuildEmailException(string message, Exception ex) : base(message, ex)
        {
        }
    }
}
