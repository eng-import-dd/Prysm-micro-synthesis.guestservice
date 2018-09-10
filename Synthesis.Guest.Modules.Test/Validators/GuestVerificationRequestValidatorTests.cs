using System;
using Synthesis.GuestService.InternalApi.Requests;
using Synthesis.GuestService.Modules.Test.TestData;
using Synthesis.GuestService.Validators;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Validators
{
    public class GuestVerificationRequestValidatorTests
    {
        private readonly GuestVerificationRequestValidator _validator = new GuestVerificationRequestValidator();
        
        [Fact]
        public void ShouldPassForValidRequest()
        {
            var request = new GuestVerificationRequest
            {
                ProjectAccessCode = Guid.NewGuid().ToString(),
                ProjectId = Guid.NewGuid(),
                Username = "name@domain.com"
            };

            var result = _validator.Validate(request);

            Assert.True(result.IsValid);
        }

        [Theory]
        [ClassData(typeof(GuestVerificationRequestEmailAddressGenerator))]
        public void ShouldFailOnInvalidEmailInAddressList(GuestVerificationRequest request)
        {
            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("\t ")]
        [InlineData("\f ")]
        [InlineData("\n ")]
        [InlineData("\r ")]
        public void ShouldFailForEmptyProjectIdWhenAccessCodeMissing(string accessCode)
        {
            var request = new GuestVerificationRequest
            {
                ProjectAccessCode = accessCode,
                ProjectId = Guid.Empty,
                Username = "name@domain.com"
            };

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("\t ")]
        [InlineData("\f ")]
        [InlineData("\n ")]
        [InlineData("\r ")]
        public void ShouldPassWithValidProjectIdAndMissingAccessCode(string accessCode)
        {
            var request = new GuestVerificationRequest
            {
                ProjectAccessCode = accessCode,
                ProjectId = Guid.NewGuid(),
                Username = "name@domain.com"
            };

            var result = _validator.Validate(request);

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("0123456789")]
        [InlineData("1053D337 - BA8E - 43D6 - 9AFC - EB2A04BA4A33")]
        [InlineData("1053D337-BA8E-43D6-9AFC-EB2A04BA4A33")]
        public void ShouldPassWithEmptyProjectIdAndValidAccessCode(string accessCode)
        {
            var request = new GuestVerificationRequest
            {
                ProjectAccessCode = accessCode,
                ProjectId = Guid.Empty,
                Username = "name@domain.com"
            };

            var result = _validator.Validate(request);

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("a123456789")]
        [InlineData("01234567890")]
        [InlineData("123456789")]
        [InlineData("Y053D337-BA8E-43D6-9AFC-EB2A04BA4A33")]
        [InlineData("1053D337-BA8E-43D6-9AFC-EB2A04BA4A339")]
        [InlineData("1053D337-BA8E-43D6-9AFC-EB2A04BA4A3")]
        [InlineData("00000000-0000-0000-0000-000000000000")]
        public void ShouldFailForValidProjectIdAndInvalidAccessCode(string accessCode)
        {
            var request = new GuestVerificationRequest
            {
                ProjectAccessCode = accessCode,
                ProjectId = Guid.NewGuid(),
                Username = "name@domain.com"
            };

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("a123456789")]
        [InlineData("01234567890")]
        [InlineData("123456789")]
        [InlineData("Y053D337-BA8E-43D6-9AFC-EB2A04BA4A33")]
        [InlineData("1053D337-BA8E-43D6-9AFC-EB2A04BA4A339")]
        [InlineData("1053D337-BA8E-43D6-9AFC-EB2A04BA4A3")]
        [InlineData("00000000-0000-0000-0000-000000000000")]
        public void ShouldFailForEmptyProjectIdAndInvalidAccessCode(string accessCode)
        {
            var request = new GuestVerificationRequest
            {
                ProjectAccessCode = accessCode,
                ProjectId = Guid.Empty,
                Username = "name@domain.com"
            };

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }
    }
}
