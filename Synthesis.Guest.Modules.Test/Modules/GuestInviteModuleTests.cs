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
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Entity;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PolicyEvaluator;
using Synthesis.PolicyEvaluator.Models;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Modules
{
    public class GuestInviteModuleTests
    {
        private readonly ValidationFailure _expectedValidationFailure = new ValidationFailure("theprop", "thereason");
        private readonly GuestInvite _guestInvite = new GuestInvite { Id = Guid.NewGuid(), InvitedBy = Guid.NewGuid(), ProjectId = Guid.NewGuid(), CreatedDateTime = DateTime.UtcNow };
        private readonly Mock<IGuestInviteController> _guestInviteControllerMock = new Mock<IGuestInviteController>();
        private readonly Mock<IPolicyEvaluator> _policyEvaluatorMock = new Mock<IPolicyEvaluator>();
        private readonly Mock<ITokenValidator> _tokenValidatorMock = new Mock<ITokenValidator>();
        private readonly Mock<IMetadataRegistry> _metadataRegistryMock = new Mock<IMetadataRegistry>();
        private readonly Mock<ILoggerFactory> _loggerFactoryMock = new Mock<ILoggerFactory>();

        public GuestInviteModuleTests()
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

                with.Dependency(_guestInviteControllerMock.Object);
                with.Dependency(_metadataRegistryMock.Object);
                with.Dependency(_tokenValidatorMock.Object);
                with.Dependency(_policyEvaluatorMock.Object);
                with.Dependency(_loggerFactoryMock.Object);

                with.Module<GuestInviteModule>();
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

        private static void BuildRequest<T>(BrowserContext context, T body)
        {
            context.HttpRequest();
            context.Header("Accept", "application/json");
            context.Header("Content-Type", "application/json");
            context.JsonBody(body);
        }

        [Fact]
        public async Task GetGuestInviteReturnsUnauthorizedRequest()
        {
            _guestInviteControllerMock
                .Setup(x => x.GetGuestInviteAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new GuestInvite());

            var response = await UnauthenticatedBrowser.Get($"{Routing.GuestInvitesRoute}/{Guid.NewGuid()}", BuildRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CreateGuestInviteReturnsUnauthorizedRequest()
        {
            var response = await UnauthenticatedBrowser.Post($"{Routing.GuestInvitesRoute}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task UpdateGuestInviteReturnsUnauthorizedRequest()
        {
            _guestInviteControllerMock
                .Setup(x => x.UpdateGuestInviteAsync(It.IsAny<GuestInvite>()))
                .ReturnsAsync(new GuestInvite());

            var response = await UnauthenticatedBrowser.Put($"{Routing.GuestInvitesRoute}/{_guestInvite.Id}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestInviteByIdReturnsOk()
        {
            _guestInviteControllerMock
                .Setup(x => x.GetGuestInviteAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new GuestInvite());

            var response = await AuthenticatedBrowser.Get($"{Routing.GuestInvitesRoute}/{Guid.NewGuid()}", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestInviteByIdReturnsInternalServerErrorOnUnexpectedException()
        {
            _guestInviteControllerMock
                .Setup(x => x.GetGuestInviteAsync(It.IsAny<Guid>()))
                .Throws<Exception>();

            var response = await AuthenticatedBrowser.Get($"{Routing.GuestInvitesRoute}/{Guid.NewGuid()}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestInviteByIdReturnsBadRequestValidationFailedException()
        {
            _guestInviteControllerMock
                .Setup(x => x.GetGuestInviteAsync(It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure> { _expectedValidationFailure }));

            var response = await AuthenticatedBrowser.Get($"{Routing.GuestInvitesRoute}/{Guid.NewGuid()}", ctx => BuildRequest(ctx, _guestInvite));

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
        public async Task GetGuestInviteByIdReturnsNotFoundException()
        {
            _guestInviteControllerMock
                .Setup(x => x.GetGuestInviteAsync(It.IsAny<Guid>()))
                .Throws(new NotFoundException("GuestInvite not found"));

            var response = await AuthenticatedBrowser.Get($"{Routing.GuestInvitesRoute}/{Guid.NewGuid()}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task CreateGuestInviteReturnsOk()
        {
            var response = await AuthenticatedBrowser.Post($"{Routing.GuestInvitesRoute}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CreateGuestInviteReturnsInternalServerErrorOnUnexpectedException()
        {
            _guestInviteControllerMock
                .Setup(x => x.CreateGuestInviteAsync(It.IsAny<GuestInvite>()))
                .Throws<Exception>();

            var response = await AuthenticatedBrowser.Post($"{Routing.GuestInvitesRoute}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CreateGuestInviteReturnsBadRequestValidationFailedException()
        {
            _guestInviteControllerMock
                .Setup(x => x.CreateGuestInviteAsync(It.IsAny<GuestInvite>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure> { _expectedValidationFailure }));

            var response = await AuthenticatedBrowser.Post($"{Routing.GuestInvitesRoute}", ctx => BuildRequest(ctx, _guestInvite));

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
        public async Task UpdateGuestInviteReturnsOk()
        {
            _guestInviteControllerMock
                .Setup(x => x.UpdateGuestInviteAsync(It.IsAny<GuestInvite>()))
                .ReturnsAsync(new GuestInvite());

            var response = await AuthenticatedBrowser.Put($"{Routing.GuestInvitesRoute}/{_guestInvite.Id}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task UpdateGuestInviteReturnsInternalServerErrorOnUnexpectedException()
        {
            _guestInviteControllerMock
                .Setup(x => x.UpdateGuestInviteAsync(It.IsAny<GuestInvite>()))
                .Throws<Exception>();

            var response = await AuthenticatedBrowser.Put($"{Routing.GuestInvitesRoute}/{_guestInvite.Id}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CreateGuestInviteAsyncWithoutAccessReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Post(Routing.GuestInvitesRoute, BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestInviteAsyncWithoutAccessReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Get($"{Routing.GuestInvitesRoute}/{Guid.NewGuid()}", BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestInvitesByProjectIdAsyncWithoutAccessReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.GuestInvitesPath}", BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task UpdateGuestInviteAsyncWithoutAccessReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Put($"{Routing.GuestInvitesRoute}/{Guid.NewGuid()}", BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }
}