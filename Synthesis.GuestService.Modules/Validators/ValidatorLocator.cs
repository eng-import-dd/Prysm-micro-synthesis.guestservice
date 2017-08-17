using FluentValidation;
using Nancy.TinyIoc;
using System;

namespace Synthesis.GuestService.Validators
{
    public class ValidatorLocator : IValidatorLocator
    {
        private readonly TinyIoCContainer _container;

        public ValidatorLocator(TinyIoCContainer container)
        {
            _container = container;
        }

        public IValidator GetValidator(Type validatorType)
        {
            object validator;
            if (_container.TryResolve(validatorType, out validator))
            {
                if (validator is IValidator)
                {
                    return (IValidator)validator;
                }
            }

            return null;
        }
    }
}
