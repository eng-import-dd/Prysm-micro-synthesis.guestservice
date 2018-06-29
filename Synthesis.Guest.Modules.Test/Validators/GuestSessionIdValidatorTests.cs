using System;
using Synthesis.GuestService.Validators;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Validators
{
    public class GuestSessionIdValidatorTests
    {
        private readonly GuestSessionIdValidator _validator = new GuestSessionIdValidator();

        [Fact]
        public void ShouldPassForValidId()
        {
            var result = _validator.Validate(Guid.NewGuid());

            Assert.True(result.IsValid);
        }

        [Fact]
        public void ShouldFailForInvalidId()
        {
            var result = _validator.Validate(Guid.Empty);

            Assert.False(result.IsValid);
        }
    }
}
