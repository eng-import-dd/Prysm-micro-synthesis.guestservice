using System;
using System.Collections.Generic;
using Moq;
using Nancy;
using Synthesis.GuestService.Controllers;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Modules
{
    public class GuestTenantModuleTests : BaseModuleTests<GuestTenantModule>
    {
        private readonly Mock<IGuestTenantController> _guestTenantControllerMock = new Mock<IGuestTenantController>();
        private readonly List<Guid> _tenantIds = new List<Guid>();
        public GuestTenantModuleTests()
        {
            _guestTenantControllerMock
                .Setup(x => x.GetTenantIdsForUserAsync(It.IsAny<Guid>()))
                .ReturnsAsync(_tenantIds);
        }

        [Fact]
        public async void GetTenantIdListReturnsOk()
        {
            var response = await UserTokenBrowser.Get("guesttenantids", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async void GetTenantIdListReturns500UponControllerRaisesException()
        {
            var tenantId = Guid.NewGuid();
            _tenantIds.Add(tenantId);

            _guestTenantControllerMock
                .Setup(x => x.GetTenantIdsForUserAsync(It.IsAny<Guid>()))
                .Throws<Exception>();

            var response = await UserTokenBrowser.Get("guesttenantids", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        protected override List<object> BrowserDependencies => new List<object> { _guestTenantControllerMock.Object };
    }
}