using System;
using System.Collections.Generic;
using FluentValidation.Results;

namespace Synthesis.GuestService.Exceptions
{
    public class ValidationFailedException : Exception
    {
        public IEnumerable<ValidationFailure> Errors { get; }

        public ValidationFailedException(IEnumerable<ValidationFailure> errors) : base("Validation failed")
        {
            Errors = errors;
        }
    }
}
