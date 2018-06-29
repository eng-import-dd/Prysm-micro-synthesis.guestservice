using System;
using Synthesis.GuestService.Validators;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Validators
{
    public class GuestInviteValidatorTests2
    {
        private readonly GuestInviteIdValidator _validator = new GuestInviteIdValidator();

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
