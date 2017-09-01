using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation;

namespace Synthesis.GuestService.Validators
{
    public class ValidatorLocator : IValidatorLocator
    {
        private readonly IEnumerable<IValidator> _validators;

        public ValidatorLocator(IEnumerable<IValidator> validators)
        {
            _validators = validators;
        }

        public IValidator GetValidator(Type validatorType)
        {
            return _validators.FirstOrDefault(x => x.GetType() == validatorType);
        }
    }
}
