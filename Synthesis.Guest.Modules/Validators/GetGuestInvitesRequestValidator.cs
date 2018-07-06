using System;
using FluentValidation;
using Synthesis.GuestService.InternalApi.Models;

namespace Synthesis.GuestService.Validators
{
    public class GetGuestInvitesRequestValidator : AbstractValidator<GetGuestInvitesRequest>
    {
        public GetGuestInvitesRequestValidator()
        {
            RuleFor(request => request.GuestEmail)
                .NotNull().When(request => request.GuestUserId == null)
                .WithMessage("An email address must be specified if no UserId is provided");

            RuleFor(request => request.GuestUserId)
                .NotEmpty().When(request => request.GuestEmail == null)
                .WithMessage("A UserId must be specified if no email address is provided");
        }
    }
}