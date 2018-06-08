using FluentValidation;

namespace Synthesis.GuestService.Validators
{
    public abstract class StringValidator : AbstractValidator<string>
    {
        protected StringValidator(string name)
        {
            RuleFor(value => value.Trim()).NotEqual(string.Empty).WithMessage($"The {nameof(name)} must not be empty or whitepace");
        }
    }
}