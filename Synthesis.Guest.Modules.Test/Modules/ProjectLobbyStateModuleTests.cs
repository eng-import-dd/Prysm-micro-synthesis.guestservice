using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentValidation.Results;
using Moq;
using Nancy;
using Nancy.Testing;
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

        [Fact]
        public async Task GetProjectLobbyStateAsyncReturnsOk()
        {
            var response = await UserTokenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetProjectLobbyStateAsyncRetrievesProjectLobbyState()
        {
            await UserTokenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            _projectLobbyStateControllerMock.Verify(m => m.GetProjectLobbyStateAsync(It.IsAny<Guid>()));
        }

        [Fact]
        public async Task GetProjectLobbyStateAsyncReturnsUnauthorizedIfNotAuthenticated()
        {
            var response = await UnauthenticatedBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetProjectLobbyStateAsyncWithoutAccessReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetProjectLobbyStateAsyncReturnsBadRequestIfValidationFailedExceptionIsThrown()
        {
            _projectLobbyStateControllerMock
                .Setup(m => m.GetProjectLobbyStateAsync(It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await UserTokenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetProjectLobbyStateAsyncReturnsNotFoundIfNotFoundExceptionIsThrown()
        {
            _projectLobbyStateControllerMock
                .Setup(m => m.GetProjectLobbyStateAsync(It.IsAny<Guid>()))
                .Throws(new NotFoundException(string.Empty));

            var response = await UserTokenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetProjectLobbyStateAsyncReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _projectLobbyStateControllerMock
                .Setup(m => m.GetProjectLobbyStateAsync(It.IsAny<Guid>()))
                .Throws(new Exception());

            var response = await UserTokenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
    }
}
