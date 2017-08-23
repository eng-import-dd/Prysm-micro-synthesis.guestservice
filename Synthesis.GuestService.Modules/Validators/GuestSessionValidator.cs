using System;
using FluentValidation;
using Synthesis.GuestService.Dao.Models;

namespace Synthesis.GuestService.Validators
{
    public class GuestSessionValidator : AbstractValidator<GuestSession>
    {
        public GuestSessionValidator()
        {
            RuleFor(request => request.Id)
                .NotEqual(Guid.Empty).WithMessage("The Id field must not be empty");

            RuleFor(request => request.UserId)
                .NotEqual(Guid.Empty).WithMessage("The UserId field must not be empty");

            RuleFor(request => request.ProjectId)
                .NotEqual(Guid.Empty).WithMessage("The ProjectId field must not be empty");

            RuleFor(request => request.ProjectAccessCode)
                .NotEqual(string.Empty).WithMessage("The ProjectAccessCode fiels must not be empty.");
        }
    }
}
