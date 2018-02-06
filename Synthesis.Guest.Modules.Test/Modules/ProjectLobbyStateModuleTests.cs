using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation.Results;
using Moq;
using Nancy;
using Nancy.Testing;
using Synthesis.Authentication;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Controllers;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.Policy.Models;
using Synthesis.PolicyEvaluator;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Modules
{
    public class ProjectLobbyStateModuleTests
    {
        private readonly Mock<IProjectLobbyStateController> _projectLobbyStateControllerMock = new Mock<IProjectLobbyStateController>();
        private readonly Mock<IPolicyEvaluator> _policyEvaluatorMock = new Mock<IPolicyEvaluator>();
        private readonly Mock<ITokenValidator> _tokenValidatorMock = new Mock<ITokenValidator>();
        private readonly Mock<IMetadataRegistry> _metadataRegistryMock = new Mock<IMetadataRegistry>();
        private readonly Mock<ILoggerFactory> _loggerFactoryMock = new Mock<ILoggerFactory>();

        public ProjectLobbyStateModuleTests()
        {
            _loggerFactoryMock.Setup(m => m.Get(It.IsAny<LogTopic>()))
                .Returns(new Mock<ILogger>().Object);

            _metadataRegistryMock
                .Setup(x => x.GetRouteMetadata(It.IsAny<string>()))
                .Returns<string>(name => new SynthesisRouteMetadata(null, null, name));
        }

        private Browser GetBrowser(bool isAuthenticated = true, bool hasAccess = true)
        {
            _policyEvaluatorMock
                .Setup(x => x.EvaluateAsync(It.IsAny<PolicyEvaluationContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(hasAccess ? PermissionScope.Allow : PermissionScope.Deny);

            return new Browser(with =>
            {
                if (isAuthenticated)
                {
                    var identity = new ClaimsIdentity(new[]
                           {
                                new Claim(ClaimTypes.Name, "Test User"),
                                new Claim(ClaimTypes.Email, "test@user.com")
                            },
                           AuthenticationTypes.Basic);

                    _tokenValidatorMock
                        .Setup(x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>()))
                        .ReturnsAsync(new ClaimsPrincipal(identity));

                    with.RequestStartup((container, pipelines, context) =>
                    {
                        context.CurrentUser = new ClaimsPrincipal(identity);
                    });
                }

                with.Dependency(_projectLobbyStateControllerMock.Object);
                with.Dependency(_metadataRegistryMock.Object);
                with.Dependency(_tokenValidatorMock.Object);
                with.Dependency(_policyEvaluatorMock.Object);
                with.Dependency(_loggerFactoryMock.Object);

                with.Module<ProjectLobbyStateModule>();
                with.EnableAutoRegistration();
            });
        }

        private Browser AuthenticatedBrowser => GetBrowser();

        private Browser UnauthenticatedBrowser => GetBrowser(false);

        private Browser ForbiddenBrowser => GetBrowser(true, false);

        private static void BuildRequest(BrowserContext context)
        {
            context.HttpRequest();
            context.Header("Accept", "application/json");
            context.Header("Content-Type", "application/json");
        }

        [Fact]
        public async Task GetProjectLobbyStateAsyncReturnsOk()
        {
            var response = await AuthenticatedBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetProjectLobbyStateAsyncRetrievesProjectLobbyState()
        {
            await AuthenticatedBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
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

            var response = await AuthenticatedBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetProjectLobbyStateAsyncReturnsNotFoundIfNotFoundExceptionIsThrown()
        {
            _projectLobbyStateControllerMock
                .Setup(m => m.GetProjectLobbyStateAsync(It.IsAny<Guid>()))
                .Throws(new NotFoundException(string.Empty));

            var response = await AuthenticatedBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetProjectLobbyStateAsyncReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _projectLobbyStateControllerMock
                .Setup(m => m.GetProjectLobbyStateAsync(It.IsAny<Guid>()))
                .Throws(new Exception());

            var response = await AuthenticatedBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectLobbyStatePath}", BuildRequest);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
    }
}
