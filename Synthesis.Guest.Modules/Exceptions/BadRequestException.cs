using System;
using Synthesis.Http.Microservice.Models;

namespace Synthesis.GuestService.Exceptions
{
    public class BadRequestException : Exception
    {
        public BadRequestException(string message) : base(message)
        {
        }
    }
}