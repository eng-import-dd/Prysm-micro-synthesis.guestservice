using FluentValidation;

namespace Synthesis.GuestService.Validators
{
    public class ProjectAccessCodeValidator : AbstractValidator<string>
    {
        public ProjectAccessCodeValidator()
        {
            RuleFor(request => request)
                .Length(10).WithMessage("The project access code must be 10 characters in length");

            // TODCO: CU-598: Figure why this is causing a null ref exception
            RuleFor(request => request)
                .Must(x => int.TryParse(x, out var _))
                .WithMessage("The project access code must be a number");
        }
    }
}