using System;
using FluentValidation;
using Synthesis.GuestService.InternalApi.Models;

namespace Synthesis.GuestService.Validators
{
    public class GetGuestInvitesRequestValidator : AbstractValidator<GetGuestInvitesRequest>
    {
        public GetGuestInvitesRequestValidator()
        {
            RuleFor(request => request.GuestEmail).NotEmpty().When(request => request.GuestUserId == null);
            RuleFor(request => request.GuestUserId).NotEmpty().When(request => request.GuestEmail == null);
        }
    }
}