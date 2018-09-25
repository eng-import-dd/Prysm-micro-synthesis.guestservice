using System;
using FluentValidation;

namespace Synthesis.GuestService.Validators
{
    public class ProjectAccessCodeValidator : AbstractValidator<string>
    {
        public ProjectAccessCodeValidator()
        {
            RuleFor(str => str)
                .NotEmpty()
                .WithMessage("The ProjectAccessCode must not be empty");

            RuleFor(str => str)
                .Must(x => (x.Length == 10 && long.TryParse(x, out var _)) || (Guid.TryParse(x, out var _) && x != Guid.Empty.ToString()))
                .WithMessage("The ProjectAccessCode must be a 10 digit number or a valid non-empty GUID");
        }
    }
}
