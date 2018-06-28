using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.Validators;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Validators
{
    public class GetGuestInvitesRequestValidatorTests
    {
        private readonly GetGuestInvitesRequestValidator _validator = new GetGuestInvitesRequestValidator();
        private readonly GetGuestInvitesRequest _defaultRequest = GetGuestInvitesRequest.Example();

        [Fact]
        public void ShouldPassForValidRequest()
        {
            var result = _validator.Validate(_defaultRequest);

            Assert.True(result.IsValid);
        }
    }
}
