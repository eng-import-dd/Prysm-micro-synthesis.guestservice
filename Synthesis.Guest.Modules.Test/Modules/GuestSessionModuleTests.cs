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
using Synthesis.GuestService.Models;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Entity;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PolicyEvaluator;
using Synthesis.PolicyEvaluator.Models;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Modules
{
    public class GuestSessionModuleTests
    {
        private readonly ValidationFailure _expectedValidationFailure = new ValidationFailure("theprop", "thereason");
        private readonly GuestSession _guestSession = new GuestSession { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ProjectId = Guid.NewGuid(), ProjectAccessCode = "12345" };
        private readonly Mock<IGuestSessionController> _guestSessionControllerMock = new Mock<IGuestSessionController>();
        private readonly Mock<IPolicyEvaluator> _policyEvaluatorMock = new Mock<IPolicyEvaluator>();
        private readonly Mock<IPolicyEvaluator> _policyEvaluatorForbiddenMock = new Mock<IPolicyEvaluator>();
        private readonly Mock<ITokenValidator> _tokenValidatorMock = new Mock<ITokenValidator>();
        private readonly Mock<IMetadataRegistry> _metadataRegistryMock = new Mock<IMetadataRegistry>();
        private readonly Mock<ILoggerFactory> _loggerFactoryMock = new Mock<ILoggerFactory>();

        public GuestSessionModuleTests()
        {
            _loggerFactoryMock.Setup(m => m.Get(It.IsAny<LogTopic>()))
                .Returns(new Mock<ILogger>().Object);

            _policyEvaluatorForbiddenMock
                .Setup(x => x.EvaluateAsync(It.IsAny<PolicyEvaluationContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PermissionScope.Deny);

            _metadataRegistryMock
                .Setup(x => x.GetRouteMetadata(It.IsAny<string>()))
                .Returns<string>(name => new SynthesisRouteMetadata(null, null, name));
        }

        private Browser GetBrowser(bool isAuthenticated = true, bool hasAccess = true)
        {
            return new Browser(with =>
            {
                if (isAuthenticated)
                {
                    with.RequestStartup((container, pipelines, context) =>
                    {
                        var identity = new ClaimsIdentity(new[]
                            {
                                new Claim(ClaimTypes.Name, "Test User"),
                                new Claim(ClaimTypes.Email, "test@user.com")
                            },
                            AuthenticationTypes.Basic);
                        context.CurrentUser = new ClaimsPrincipal(identity);
                    });
                }

                with.Dependency(_guestSessionControllerMock.Object);
                with.Dependency(_metadataRegistryMock.Object);
                with.Dependency(_tokenValidatorMock.Object);
                with.Dependency(hasAccess ? _policyEvaluatorMock.Object : _policyEvaluatorForbiddenMock.Object);
                with.Dependency(_loggerFactoryMock.Object);

                with.Module<GuestSessionModule>();
                with.EnableAutoRegistration();
            });
        }

        private Browser AuthenticatedBrowser => GetBrowser();

        private Browser UnauthenticatedBrowser => GetBrowser(false);

        private Browser ForbiddenBrowser => GetBrowser(true, false);

        private void BuildRequest(BrowserContext context)
        {
            context.HttpRequest();
            context.Header("Accept", "application/json");
            context.Header("Content-Type", "application/json");
        }

        private void BuildRequest<T>(BrowserContext context, T body)
        {
            context.HttpRequest();
            context.Header("Accept", "application/json");
            context.Header("Content-Type", "application/json");
            context.JsonBody(body);
        }

        [Fact]
        public async Task GetGuestSessionReturnsUnauthorizedRequest()
        {
            _guestSessionControllerMock
                .Setup(x => x.GetGuestSessionAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new GuestSession());

            var response = await UnauthenticatedBrowser.Get($"{Routing.GuestSessionsRoute}/{Guid.NewGuid()}", BuildRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CreateGuestSessionReturnsUnauthorizedRequest()
        {
            var response = await UnauthenticatedBrowser.Post($"{Routing.GuestSessionsRoute}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task UpdateGuestSessionReturnsUnauthorizedRequest()
        {
            _guestSessionControllerMock
                .Setup(x => x.UpdateGuestSessionAsync(It.IsAny<GuestSession>()))
                .ReturnsAsync(new GuestSession());
            var response = await UnauthenticatedBrowser.Put($"{Routing.GuestSessionsRoute}/{_guestSession.Id}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestSessionByIdReturnsOk()
        {
            _guestSessionControllerMock
                .Setup(x => x.GetGuestSessionAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new GuestSession());

            var response = await AuthenticatedBrowser.Get($"{Routing.GuestSessionsRoute}/{Guid.NewGuid()}", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestSessionByIdReturnsInternalServerErrorOnUnexpectedException()
        {
            _guestSessionControllerMock
                .Setup(x => x.GetGuestSessionAsync(It.IsAny<Guid>()))
                .Throws<Exception>();

            var response = await AuthenticatedBrowser.Get($"{Routing.GuestSessionsRoute}/{Guid.NewGuid()}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestSessionByIdReturnsBadRequestValidationFailedException()
        {
            _guestSessionControllerMock
                .Setup(x => x.GetGuestSessionAsync(It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure> { _expectedValidationFailure }));

            var response = await AuthenticatedBrowser.Get($"{Routing.GuestSessionsRoute}/{Guid.NewGuid()}", ctx => BuildRequest(ctx, _guestSession));

            var failedResponse = response.Body.DeserializeJson<FailedResponse>();
            Assert.NotNull(failedResponse?.Errors);

            Assert.Collection(failedResponse.Errors,
                item =>
                {
                    Assert.Equal(_expectedValidationFailure.PropertyName, item.PropertyName);
                    Assert.Equal(_expectedValidationFailure.ErrorMessage, item.Message);
                });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateGuestSessionReturnsOk()
        {
            var response = await AuthenticatedBrowser.Post($"{Routing.GuestSessionsRoute}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CreateGuestSessionReturnsInternalServerErrorOnUnexpectedException()
        {
            _guestSessionControllerMock
                .Setup(x => x.CreateGuestSessionAsync(It.IsAny<GuestSession>()))
                .Throws<Exception>();

            var response = await AuthenticatedBrowser.Post($"{Routing.GuestSessionsRoute}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CreateGuestSessionReturnsBadRequestValidationFailedException()
        {
            _guestSessionControllerMock
                .Setup(x => x.CreateGuestSessionAsync(It.IsAny<GuestSession>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure> { _expectedValidationFailure }));

            var response = await AuthenticatedBrowser.Post($"{Routing.GuestSessionsRoute}", ctx => BuildRequest(ctx, _guestSession));

            var failedResponse = response.Body.DeserializeJson<FailedResponse>();
            Assert.NotNull(failedResponse?.Errors);

            Assert.Collection(failedResponse.Errors,
                item =>
                {
                    Assert.Equal(_expectedValidationFailure.PropertyName, item.PropertyName);
                    Assert.Equal(_expectedValidationFailure.ErrorMessage, item.Message);
                });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UpdateGuestSessionReturnsOk()
        {
            _guestSessionControllerMock
                .Setup(x => x.UpdateGuestSessionAsync(It.IsAny<GuestSession>()))
                .ReturnsAsync(new GuestSession());

            var response = await AuthenticatedBrowser.Put($"{Routing.GuestSessionsRoute}/{_guestSession.Id}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task UpdateGuestSessionReturnsInternalServerErrorOnUnexpectedException()
        {
            _guestSessionControllerMock
                .Setup(x => x.UpdateGuestSessionAsync(It.IsAny<GuestSession>()))
                .Throws<Exception>();

            var response = await AuthenticatedBrowser.Put($"{Routing.GuestSessionsRoute}/{_guestSession.Id}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CreateGuestInviteAsyncWithoutAccessReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Post(Routing.GuestSessionsRoute, BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestSessionAsyncWithoutAccessReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Get($"{Routing.GuestSessionsRoute}/{Guid.NewGuid()}", BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task UpdateGuestSessionAsyncWithoutAccessReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Put($"{Routing.GuestSessionsRoute}/{Guid.NewGuid()}", BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestSessionsByProjectIdAsyncWithoutAccessReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.GuestSessionsPath}", BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetProjectStatusAsyncWithoutAccessReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.ProjectStatusPath}", BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task SendVerificationEmailAsyncWithoutAccessReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Post(Routing.VerificationEmailRoute, BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task VerifyGuestAsyncWithoutAccessReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Post(Routing.VerifyGuestRoute, BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}