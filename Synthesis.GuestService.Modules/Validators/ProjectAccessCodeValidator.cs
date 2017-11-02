using FluentValidation;

namespace Synthesis.GuestService.Validators
{
    public class ProjectAccessCodeValidator : StringValidator
    {
        public ProjectAccessCodeValidator() : base("ProjectAccessCode")
        {
            RuleFor(request => request)
                .Length(10).WithMessage("The ProjectAccessCode field must be 10 characters in length");
            RuleFor(request => request)
                .Must(x => int.TryParse(x, out var _))
                .WithMessage("The access code must be a number");
        }
    }
}