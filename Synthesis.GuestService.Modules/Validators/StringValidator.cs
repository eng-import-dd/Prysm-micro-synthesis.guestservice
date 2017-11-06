using FluentValidation;
using System;

namespace Synthesis.GuestService.Validators
{
    public abstract class StringValidator : AbstractValidator<string>
    {
        protected StringValidator(string name)
        {
            RuleFor(guid => guid).NotEqual(string.Empty).WithMessage($"The {nameof(name)} must not be empty");
        }
    }
}