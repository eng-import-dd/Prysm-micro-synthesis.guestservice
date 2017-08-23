using System;
using FluentValidation;

namespace Synthesis.GuestService.Validators
{
    public interface IValidatorLocator
    {
        IValidator GetValidator(Type validatorType);
    }
}
