using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentValidation.Results;
using Moq;
using Nancy;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.InternalApi.Constants;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Modules
{
    public class ProjectLobbyStateModuleTests : BaseModuleTests<ProjectLobbyStateModule>
    {
        private readonly Mock<IProjectLobbyStateController> _projectLobbyStateControllerMock = new Mock<IProjectLobbyStateController>();

        protected override List<object> BrowserDependencies => new List<object> { _projectLobbyStateControllerMock.Object };

        #region GetProjectLobbyStateAsync
        [Fact]
        public async Task GetProjectLobbyStateAsync_ReturnsOk()
        {
            var response = await UserTokenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetProjectLobbyStateAsync_RetrievesProjectLobbyState()
        {
            await UserTokenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            _projectLobbyStateControllerMock.Verify(m => m.GetProjectLobbyStateAsync(It.IsAny<Guid>()));
        }

        [Fact]
        public async Task GetProjectLobbyStateAsync_IfNotAuthenticated_ReturnsUnauthorized()
        {
            var response = await UnauthenticatedBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetProjectLobbyStateAsync_WithoutAccessReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetProjectLobbyStateAsync_IfValidationFailedExceptionIsThrown_ReturnsBadRequest()
        {
            _projectLobbyStateControllerMock
                .Setup(m => m.GetProjectLobbyStateAsync(It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await UserTokenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetProjectLobbyStateAsync_IfNotFoundExceptionIsThrown_ReturnsNotFound()
        {
            _projectLobbyStateControllerMock
                .Setup(m => m.GetProjectLobbyStateAsync(It.IsAny<Guid>()))
                .Throws(new NotFoundException(string.Empty));

            var response = await UserTokenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetProjectLobbyStateAsync_IfUnhandledExceptionIsThrown_ReturnsInternalServerError()
        {
            _projectLobbyStateControllerMock
                .Setup(m => m.GetProjectLobbyStateAsync(It.IsAny<Guid>()))
                .Throws(new Exception());

            var response = await UserTokenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        #endregion GetProjectLobbyStateAsync

        #region RecalculateProjectLobbyStateAsync
        [Fact]
        public async Task RecalculateProjectLobbyStateAsync_ReturnsOk()
        {
            var response = await UserTokenBrowser.Put($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task RecalculateProjectLobbyStateAsync_RetrievesProjectLobbyState()
        {
            await UserTokenBrowser.Put($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            _projectLobbyStateControllerMock.Verify(m => m.RecalculateProjectLobbyStateAsync(It.IsAny<Guid>()));
        }

        [Fact]
        public async Task RecalculateProjectLobbyStateAsync_IfNotAuthenticated_ReturnsUnauthorized()
        {
            var response = await UnauthenticatedBrowser.Put($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task RecalculateProjectLobbyStateAsync_WithoutAccess_ReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Put($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task RecalculateProjectLobbyStateAsync_ValidationFailedExceptionIsThrown_ReturnsBadRequest()
        {
            _projectLobbyStateControllerMock
                .Setup(m => m.RecalculateProjectLobbyStateAsync(It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await UserTokenBrowser.Put($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task RecalculateProjectLobbyStateAsync_IfNotFoundExceptionThrown_ReturnsNotFound()
        {
            _projectLobbyStateControllerMock
                .Setup(m => m.RecalculateProjectLobbyStateAsync(It.IsAny<Guid>()))
                .Throws(new NotFoundException(string.Empty));

            var response = await UserTokenBrowser.Put($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task RecalculateProjectLobbyStateAsync_IfUnhandledExceptionThrown_ReturnsInternalServerError()
        {
            _projectLobbyStateControllerMock
                .Setup(m => m.RecalculateProjectLobbyStateAsync(It.IsAny<Guid>()))
                .Throws(new Exception());

            var response = await UserTokenBrowser.Put($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        #endregion RecalculateProjectLobbyStateAsync
    }
}
