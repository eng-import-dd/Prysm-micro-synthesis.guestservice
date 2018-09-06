using System;
using FluentValidation;
using Synthesis.GuestService.InternalApi.Requests;

namespace Synthesis.GuestService.Validators
{
    public class GuestVerificationRequestValidator : AbstractValidator<GuestVerificationRequest>
    {
        public GuestVerificationRequestValidator()
        {
            RuleFor(request => request.Username)
                .SetValidator(new EmailValidator());

            RuleFor(request => request.ProjectId)
                .SetValidator(new ProjectIdValidator())
                .When(request => string.IsNullOrWhiteSpace(request.ProjectAccessCode))
                .WithMessage($"{nameof(GuestVerificationRequest.ProjectId)} cannot be empty when {nameof(GuestVerificationRequest.ProjectAccessCode)} is not provided.");

            RuleFor(request => request.ProjectAccessCode)
                .NotNull().When(request => request.ProjectId == Guid.Empty)
                .WithMessage($"{nameof(GuestVerificationRequest.ProjectAccessCode)} cannot be null when {nameof(GuestVerificationRequest.ProjectId)} is empty.")
                .NotEmpty().When(request => request.ProjectId == Guid.Empty)
                .WithMessage($"{nameof(GuestVerificationRequest.ProjectAccessCode)} cannot be empty or whitespace when {nameof(GuestVerificationRequest.ProjectId)} is empty.");

            //RuleFor(request => request.ProjectAccessCode)
            //    .SetValidator(new ProjectAccessCodeValidator())
            //    .When(request => !string.IsNullOrWhiteSpace(request.ProjectAccessCode));
        }
    }
}
