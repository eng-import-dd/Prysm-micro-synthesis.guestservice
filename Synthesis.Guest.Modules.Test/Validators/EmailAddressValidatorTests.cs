using Synthesis.EmailService.InternalApi.TestData;
using Synthesis.GuestService.Validators;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Validators
{
    public class EmailAddressValidatorTests
    {
        private readonly EmailValidator _validator = new EmailValidator();

        [Fact]
        public void ShouldPassForValidEmail()
        {
            var result = _validator.Validate("abc@xyz.com");

            Assert.True(result.IsValid);
        }

        [Theory]
        [ClassData(typeof(EmailAddressSource))]
        public void ShouldFailOnInvalidEmailInAddressList(string emailAddress)
        {
            // EmailAddressSource includes null as an invalid type.  This validator cannot handle a null object.  Only null properties on an object
            if (emailAddress == null)
            {
                return;
            }

            var result = _validator.Validate(emailAddress);

            Assert.False(result.IsValid);
        }
    }
}
