using System;
using FluentValidation;
using Synthesis.GuestService.InternalApi.Models;

namespace Synthesis.GuestService.Validators
{
    public class GetGuestInvitesRequestValidator : AbstractValidator<GetGuestInvitesRequest>
    {
        public GetGuestInvitesRequestValidator()
        {
            // Both params are optional so no need to validate...
        }
    }
}