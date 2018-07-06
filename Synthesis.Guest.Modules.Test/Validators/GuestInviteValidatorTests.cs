using System;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.Validators;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Validators
{
    public class GuestInviteValidatorTests
    {
        private readonly GuestInviteValidator _validator = new GuestInviteValidator();
        private readonly GuestInvite _defaultInvite = GuestInvite.Example();

        [Fact]
        public void ShouldPassForValidInvite()
        {
            var result = _validator.Validate(_defaultInvite);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void ShouldFailForEmptyInvitedBy()
        {
            _defaultInvite.InvitedBy = Guid.Empty;

            var result = _validator.Validate(_defaultInvite);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailForEmptyProjectId()
        {
            _defaultInvite.ProjectId = Guid.Empty;

            var result = _validator.Validate(_defaultInvite);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailForInvalidEmail()
        {
            _defaultInvite.GuestEmail = "0123";

            var result = _validator.Validate(_defaultInvite);

            Assert.False(result.IsValid);
        }
    }
}
