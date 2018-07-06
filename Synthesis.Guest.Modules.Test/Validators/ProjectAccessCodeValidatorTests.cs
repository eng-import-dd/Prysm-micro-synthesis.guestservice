using Synthesis.GuestService.Validators;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Validators
{
    public class ProjectAccessCodeValidatorTests
    {
        private readonly ProjectAccessCodeValidator _validator = new ProjectAccessCodeValidator();

        [Fact]
        public void ShouldPassForValidCode()
        {
            var result = _validator.Validate("0123456789");

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("0123")]
        [InlineData("012345678901")]
        public void ShouldFailForCodeLengthNotEqualToTen(string code)
        {
            var result = _validator.Validate(code);

            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("0123a56789")]
        [InlineData("0123dd4567")]
        public void ShouldFailForCodeWithNonNumericCharacters(string code)
        {
            var result = _validator.Validate(code);

            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("\t ")]
        [InlineData("\f ")]
        [InlineData("\n ")]
        [InlineData("\r ")]
        public void ShouldFailForEmptyOrWhitespaceCode(string code)
        {
            var result = _validator.Validate(code);

            Assert.False(result.IsValid);
        }
    }
}
