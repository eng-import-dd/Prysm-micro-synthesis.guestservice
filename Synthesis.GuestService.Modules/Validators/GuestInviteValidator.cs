using System;
using FluentValidation;
using Synthesis.GuestService.Dao.Models;

namespace Synthesis.GuestService.Validators
{
    public class GuestInviteValidator : AbstractValidator<GuestInvite>
    {
        public GuestInviteValidator()
        {
            RuleFor(request => request.Id)
                .NotEqual(Guid.Empty).WithMessage("The Id must not be empty");
        }
    }
}
