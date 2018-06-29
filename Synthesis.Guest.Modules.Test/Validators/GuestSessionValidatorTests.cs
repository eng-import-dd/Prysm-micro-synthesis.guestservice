using System;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.Validators;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Validators
{
    public class GuestSessionValidatorTests
    {
        private readonly GuestSessionValidator _validator = new GuestSessionValidator();
        private readonly GuestSession _defaultSession = GuestSession.Example();

        [Fact]
        public void ShouldPassForValidSession()
        {
            var result = _validator.Validate(_defaultSession);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void ShouldFailForEmptyUserId()
        {
            _defaultSession.UserId = Guid.Empty;

            var result = _validator.Validate(_defaultSession);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailForEmptyProjectId()
        {
            _defaultSession.ProjectId = Guid.Empty;

            var result = _validator.Validate(_defaultSession);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailForEmptyProjectAccessCode()
        {
            _defaultSession.ProjectAccessCode = "";

            var result = _validator.Validate(_defaultSession);

            Assert.False(result.IsValid);
        }
    }
}
