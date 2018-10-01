using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation.Results;
using Moq;
using Nancy;
using Nancy.Testing;
using Synthesis.Authentication;
using Synthesis.Authentication.Jwt;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.InternalApi.Constants;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.InternalApi.Requests;
using Synthesis.GuestService.InternalApi.Responses;
using Synthesis.Http.Microservice;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Entity;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.Policy.Models;
using Synthesis.PolicyEvaluator;
using Synthesis.PrincipalService.InternalApi.Api;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.ProjectService.InternalApi.Models;
using Xunit;
using ClaimTypes = Synthesis.Authentication.Jwt.ClaimTypes;
using NetHttpStatusCode = System.Net.HttpStatusCode;

// ReSharper disable ExplicitCallerInfoArgument

namespace Synthesis.GuestService.Modules.Test.Modules
{
    public class GuestSessionModuleTests
    {
        private readonly ValidationFailure _expectedValidationFailure = new ValidationFailure("theprop", "thereason");
        private readonly GuestSession _guestSession = new GuestSession { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ProjectId = Guid.NewGuid(), ProjectAccessCode = Guid.NewGuid().ToString() };
        private readonly Mock<IGuestSessionController> _guestSessionControllerMock = new Mock<IGuestSessionController>();
        private readonly Mock<IUserApi> _userApiMock = new Mock<IUserApi>();
        private readonly Mock<IPolicyEvaluator> _policyEvaluatorMock = new Mock<IPolicyEvaluator>();
        private readonly Mock<ITokenValidator> _tokenValidatorMock = new Mock<ITokenValidator>();
        private readonly Mock<IMetadataRegistry> _metadataRegistryMock = new Mock<IMetadataRegistry>();
        private readonly Mock<ILoggerFactory> _loggerFactoryMock = new Mock<ILoggerFactory>();
        private readonly Guid _tenantId = Guid.NewGuid();
        private readonly Guid _principalId = Guid.NewGuid();
        private readonly User _defaultGuestUser;
        private readonly Project _defaultProject = Project.Example();
        private readonly GuestVerificationRequest _defaultGuestVerificationRequest;

        public GuestSessionModuleTests()
        {
            _loggerFactoryMock.Setup(m => m.Get(It.IsAny<LogTopic>()))
                .Returns(new Mock<ILogger>().Object);

            _metadataRegistryMock
                .Setup(x => x.GetRouteMetadata(It.IsAny<string>()))
                .Returns<string>(name => new SynthesisRouteMetadata(null, null, name));

            _defaultGuestUser = User.GuestUserExample();
            _defaultGuestUser.Id = _principalId;
            _defaultGuestUser.IsLocked = false;
            _defaultGuestUser.IsEmailVerified = true;
            _defaultGuestUser.EmailVerifiedAt = DateTime.Now.AddDays(-1.0);

            _defaultProject.TenantId = _tenantId;

            _defaultGuestVerificationRequest = new GuestVerificationRequest() { ProjectAccessCode = _defaultProject.GuestAccessCode, ProjectId = _defaultProject.Id, Username = _defaultGuestUser.Username };
        }

        //TODO: Add UpdateGuestSessionState route tests

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
                               new Claim(JwtRegisteredClaimNames.Aud, Audiences.Synthesis),
                               new Claim(JwtRegisteredClaimNames.Sub, _principalId.ToString()),
                               new Claim(ClaimTypes.Tenant, _tenantId.ToString()),
                               new Claim(ClaimTypes.Group, Guid.NewGuid().ToString()),
                               new Claim(ClaimTypes.Group, Guid.NewGuid().ToString())
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

                with.Dependency(_guestSessionControllerMock.Object);
                with.Dependency(_metadataRegistryMock.Object);
                with.Dependency(_tokenValidatorMock.Object);
                with.Dependency(_policyEvaluatorMock.Object);
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
        public async Task VerifyGuestAsync_CallsVerifyGuestAsyncControllerMethodWithExpectedArgs()
        {
             _userApiMock
                .Setup(x => x.GetUserByUsernameAsync(_defaultGuestUser.Username))
                .ReturnsAsync(MicroserviceResponse.Create(NetHttpStatusCode.OK, _defaultGuestUser));

            await AuthenticatedBrowser.Post($"{Routing.VerifyGuestRoute}", ctx => BuildRequest(ctx, _defaultGuestVerificationRequest));

            _guestSessionControllerMock
                .Verify(x => x.VerifyGuestAsync(It.Is<GuestVerificationRequest>(gvr =>
                gvr.ProjectAccessCode == _defaultGuestVerificationRequest.ProjectAccessCode &&
                gvr.ProjectId == _defaultGuestVerificationRequest.ProjectId &&
                gvr.Username == _defaultGuestVerificationRequest.Username),
                It.Is<Guid>(t => t == _tenantId)));
        }

        [Fact]
        public async Task VerifyGuestAsync_WhenHappyPath_ReturnsOk()
        {
            var response = await AuthenticatedBrowser.Post($"{Routing.VerifyGuestRoute}", ctx => BuildRequest(ctx, _defaultGuestVerificationRequest));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task VerifyGuestAsync_WhenValidationFails_ReturnsBadRequestValidationFailedException()
        {
            _guestSessionControllerMock
                .Setup(x => x.VerifyGuestAsync(It.IsAny<GuestVerificationRequest>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure> { _expectedValidationFailure }));

            var response = await AuthenticatedBrowser.Post($"{Routing.VerifyGuestRoute}", ctx => BuildRequest(ctx, _defaultGuestVerificationRequest));

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
        public async Task VerifyGuestAsync_WhenAccessCheckFails_ReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Post(Routing.VerifyGuestRoute, BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task VerifyGuestAsync_OnUnexpectedException_ReturnsInternalServerError()
        {
            _guestSessionControllerMock
                .Setup(x => x.VerifyGuestAsync(It.IsAny<GuestVerificationRequest>(), It.IsAny<Guid>()))
                .Throws<Exception>();

            var response = await AuthenticatedBrowser.Post($"{Routing.VerifyGuestRoute}", ctx => BuildRequest(ctx, _defaultGuestVerificationRequest));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task VerifyGuestAsync_WhenPrincipalNotAuthorized_ReturnsUnauthorizedRequest()
        {
            var response = await UnauthenticatedBrowser.Post($"{Routing.VerifyGuestRoute}", ctx => BuildRequest(ctx, _defaultGuestVerificationRequest));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestSession_WithoutAuthentication_ReturnsUnauthorized()
        {
            _guestSessionControllerMock
                .Setup(x => x.GetGuestSessionAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new GuestSession());

            var response = await UnauthenticatedBrowser.Get($"{Routing.GuestSessionsRoute}/{Guid.NewGuid()}", BuildRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestSession_WithoutAuthorization_ReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Get($"{Routing.GuestSessionsRoute}/{Guid.NewGuid()}", BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestSession_WhenValidationFails_ReturnsBadRequestValidationFailedException()
        {
            _guestSessionControllerMock
                .Setup(x => x.GetGuestSessionAsync(It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure> { _expectedValidationFailure }));

            var response = await AuthenticatedBrowser.Get($"{Routing.GuestSessionsRoute}/{Guid.NewGuid()}", BuildRequest);

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
        public async Task GetGuestSession_OnUnexpectedException_ReturnsInternalServerError()
        {
            _guestSessionControllerMock
                .Setup(x => x.GetGuestSessionAsync(It.IsAny<Guid>()))
                .Throws<Exception>();

            var response = await AuthenticatedBrowser.Get($"{Routing.GuestSessionsRoute}/{Guid.NewGuid()}", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestSession_ReturnsOk()
        {
            _guestSessionControllerMock
                .Setup(x => x.GetGuestSessionAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new GuestSession());

            var response = await AuthenticatedBrowser.Get($"{Routing.GuestSessionsRoute}/{Guid.NewGuid()}", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CreateGuestSession_WithoutAuthentication_ReturnsUnauthorized()
        {
            var response = await UnauthenticatedBrowser.Post($"{Routing.GuestSessionsRoute}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CreateGuestSession_WithoutAuthorization_ReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Post($"{Routing.GuestSessionsRoute}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task CreateGuestSession_OnUnexpectedException_ReturnsInternalServerError()
        {
            _guestSessionControllerMock
                .Setup(x => x.CreateGuestSessionAsync(It.IsAny<GuestSession>()))
                .Throws<Exception>();

            var response = await AuthenticatedBrowser.Post($"{Routing.GuestSessionsRoute}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CreateGuestSession_WhenValidationFails_ReturnsBadRequestValidationFailedException()
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
        public async Task CreateGuestSession_ReturnsOk()
        {
            var response = await AuthenticatedBrowser.Post($"{Routing.GuestSessionsRoute}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task UpdateGuestSession_WithoutAuthentication_ReturnsUnauthorized()
        {
            _guestSessionControllerMock
                .Setup(x => x.UpdateGuestSessionAsync(It.IsAny<GuestSession>(), It.IsAny<Guid>()))
                .ReturnsAsync(new GuestSession());
            var response = await UnauthenticatedBrowser.Put($"{Routing.GuestSessionsRoute}/{_guestSession.Id}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task UpdateGuestSession_WithoutAccess_ReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Put($"{Routing.GuestSessionsRoute}/{Guid.NewGuid()}", BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task UpdateGuestSession_ReturnsOk()
        {
            _guestSessionControllerMock
                .Setup(x => x.UpdateGuestSessionAsync(It.IsAny<GuestSession>(), It.IsAny<Guid>()))
                .ReturnsAsync(new GuestSession());

            var response = await AuthenticatedBrowser.Put($"{Routing.GuestSessionsRoute}/{_guestSession.Id}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task UpdateGuestSession_OnUnexpectedException_ReturnsInternalServerError()
        {
            _guestSessionControllerMock
                .Setup(x => x.UpdateGuestSessionAsync(It.IsAny<GuestSession>(), It.IsAny<Guid>()))
                .Throws<Exception>();

            var response = await AuthenticatedBrowser.Put($"{Routing.GuestSessionsRoute}/{_guestSession.Id}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task EmailHost_ReturnsOk()
        {
            _guestSessionControllerMock
                .Setup(x => x.EmailHostAsync(It.IsAny<string>(), It.IsAny<Guid>()))
                .ReturnsAsync(new SendHostEmailResponse());

            var response = await AuthenticatedBrowser.Get($"{Routing.GuestSessionsRoute}/{Routing.ProjectsPath}/{_guestSession.ProjectAccessCode}/{Routing.EmailHostPath}", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task EmailHost_ReturnsUnauthorizedRequest()
        {
            _guestSessionControllerMock
                .Setup(x => x.EmailHostAsync(It.IsAny<string>(), It.IsAny<Guid>()))
                .ReturnsAsync(new SendHostEmailResponse());

            var response = await UnauthenticatedBrowser.Get($"{Routing.GuestSessionsRoute}/{Routing.ProjectsPath}/{_guestSession.ProjectAccessCode}/{Routing.EmailHostPath}", BuildRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task EmailHost_OnUnexpectedException_ReturnsInternalServerError()
        {
            _guestSessionControllerMock
                .Setup(x => x.EmailHostAsync(It.IsAny<string>(), It.IsAny<Guid>()))
                .Throws<Exception>();

            var response = await AuthenticatedBrowser.Get($"{Routing.GuestSessionsRoute}/{Routing.ProjectsPath}/{_guestSession.ProjectAccessCode}/{Routing.EmailHostPath}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CreateGuestInvite_WithoutAuthorization_ReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Post(Routing.GuestSessionsRoute, BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task UpdateGuestSession_WithoutAuthorization_ReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Put($"{Routing.GuestSessionsRoute}/{Guid.NewGuid()}", BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestSessionsByProjectId_WithoutAuthorization_ReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.GuestSessionsPath}", BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestSessionsByProjectIdForCurrentUser_WithoutAuthentication_ReturnsUnauthorized()
        {
            var response = await UnauthenticatedBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.UsersPath}/{Routing.GuestSessionsPath}", BuildRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestSessionsByProjectIdForCurrentUser_WithoutAccess_ReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.UsersPath}/{Routing.GuestSessionsPath}", BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestSessionsByProjectIdForCurrentUser_OnUnexpectedExpection_ReturnsInternalServerError()
        {
            _guestSessionControllerMock
                .Setup(x => x.GetValidGuestSessionsByProjectIdForCurrentUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws<Exception>();

            var response = await AuthenticatedBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.UsersPath}/{Routing.GuestSessionsPath}", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestSessionsByProjectIdForCurrentUser_WhenValidationFails_ReturnsBadRequestValidationFailedException()
        {
            _guestSessionControllerMock
                .Setup(x => x.GetValidGuestSessionsByProjectIdForCurrentUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure> { _expectedValidationFailure }));

            var response = await AuthenticatedBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.UsersPath}/{Routing.GuestSessionsPath}",  BuildRequest);

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
        public async Task GetGuestSessionsByProjectIdForCurrentUser_ReturnsOk()
        {
            _guestSessionControllerMock
                .Setup(x => x.GetGuestSessionsByProjectIdForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(new List<GuestSession>(){ new GuestSession()});

            var response = await AuthenticatedBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.UsersPath}/{Routing.GuestSessionsPath}", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }


        [Fact]
        public async Task GetGuestSessionsByProjectIdForUser_WithoutAuthentication_ReturnsUnauthorized()
        {
            var response = await UnauthenticatedBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.UsersPath}/{Guid.NewGuid()}/{Routing.GuestSessionsPath}", BuildRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestSessionsByProjectIdForUser_WithoutAccess_ReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.UsersPath}/{Routing.GuestSessionsPath}", BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestSessionsByProjectIdForUser_OnUnexpectedExpection_ReturnsInternalServerError()
        {
            _guestSessionControllerMock
                .Setup(x => x.GetGuestSessionsByProjectIdForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws<Exception>();

            var response = await AuthenticatedBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.UsersPath}/{Guid.NewGuid()}/{Routing.GuestSessionsPath}", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestSessionsByProjectIdForUser_WhenValidationFails_ReturnsBadRequestValidationFailedException()
        {
            _guestSessionControllerMock
                .Setup(x => x.GetGuestSessionsByProjectIdForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure> { _expectedValidationFailure }));

            var response = await AuthenticatedBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.UsersPath}/{Guid.NewGuid()}/{Routing.GuestSessionsPath}", BuildRequest);

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
        public async Task GetGuestSessionsByProjectIdForUser_ReturnsOk()
        {
            _guestSessionControllerMock
                .Setup(x => x.GetGuestSessionsByProjectIdForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(new List<GuestSession>() { new GuestSession() });

            var response = await AuthenticatedBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.UsersPath}/{Guid.NewGuid()}/{Routing.GuestSessionsPath}", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
