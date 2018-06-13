using System;
using FluentValidation;
using Synthesis.GuestService.InternalApi.Models;

namespace Synthesis.GuestService.Validators
{
    public class GetGuestInvitesRequestValidator : AbstractValidator<GetGuestInvitesRequest>
    {
        public GetGuestInvitesRequestValidator()
        {
            RuleFor(request => request.GuestEmail).NotEmpty().When(request => request.GuestUserId == null).WithMessage("An email address must be specified if not UserId is provided");
            RuleFor(request => request.GuestUserId).NotEmpty().When(request => request.GuestEmail == null).WithMessage("A UserId amust be specified if no email address is provided"); ;
        }
    }
}