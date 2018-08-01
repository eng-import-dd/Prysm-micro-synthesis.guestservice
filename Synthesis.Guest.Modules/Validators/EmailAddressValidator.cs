using System.Text.RegularExpressions;
using FluentValidation;
using ValidationConstants = Synthesis.Validators.Utilities.Constants;

namespace Synthesis.GuestService.Validators
{
    public class EmailValidator : StringValidator
    {
        private readonly string _emailPropertyName;

        // this is the same Regex used in the EmailAddressAttribute class
        private static readonly Regex EmailRegex = new Regex(ValidationConstants.EmailPattern, RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.Compiled);

        public EmailValidator() : base("Email")
        {
            _emailPropertyName = "Email";
            InitializeRules();
        }

        public EmailValidator(string titleOfEmailProperty) : base(titleOfEmailProperty)
        {
            _emailPropertyName = titleOfEmailProperty;
            InitializeRules();
        }

        private void InitializeRules()
        {
            RuleFor(email => email)
                .NotNull()
                .WithMessage($"The {_emailPropertyName} address cannot be null.");

            RuleFor(email => email)
                .Must(email => !string.IsNullOrWhiteSpace(email))
                .WithMessage($"The {_emailPropertyName} address cannot be empty or whitespace.");

            RuleFor(email => email)
                .Must(IsFormatValid)
                .WithMessage($"The {_emailPropertyName} address is not properly formatted.");

        }

        public static bool IsFormatValid(string emailString)
        {
            return EmailRegex.Match(emailString).Length > 0;
        }
    }
}