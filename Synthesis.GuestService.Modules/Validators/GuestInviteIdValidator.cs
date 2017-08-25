using System;
using FluentValidation;

namespace Synthesis.GuestService.Validators
{
    public class GuestInviteIdValidator : GuidValidator
    {
        public GuestInviteIdValidator() : base("Id")
        {
            RuleFor(request => request)
                .NotEqual(Guid.Empty).WithMessage("The Id field must not be empty");
        }
    }
}