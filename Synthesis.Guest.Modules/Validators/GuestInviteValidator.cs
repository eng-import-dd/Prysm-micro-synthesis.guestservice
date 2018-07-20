using System;
using FluentValidation;
using Synthesis.GuestService.InternalApi.Models;

namespace Synthesis.GuestService.Validators
{
    public class GuestInviteValidator : AbstractValidator<GuestInvite>
    {
        public GuestInviteValidator()
        {
            RuleFor(request => request.InvitedBy)
                .NotEqual(Guid.Empty).WithMessage("The InvitedBy field must not be empty");

            RuleFor(request => request.ProjectId)
                .NotEqual(Guid.Empty).WithMessage("The ProjectId field must not be empty");

            RuleFor(request => request.GuestEmail)
                .SetValidator(new EmailValidator())
                .WithMessage("The GuestEmail address is invalid");
        }
    }
}
