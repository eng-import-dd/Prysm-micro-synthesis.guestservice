using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentValidation.Results;
using Moq;
using Nancy;
using Nancy.Testing;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.InternalApi.Constants;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.InternalApi.Requests;
using Synthesis.GuestService.InternalApi.Responses;
using Synthesis.Nancy.MicroService.Entity;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.ProjectService.InternalApi.Models;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Modules
{
    public class GuestSessionModuleTests : BaseModuleTests<GuestSessionModule>
    {
        private readonly ValidationFailure _expectedValidationFailure = new ValidationFailure("theprop", "thereason");
        private readonly GuestSession _guestSession = new GuestSession { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ProjectId = Guid.NewGuid(), ProjectAccessCode = Guid.NewGuid().ToString() };
        private readonly Mock<IGuestSessionController> _guestSessionControllerMock = new Mock<IGuestSessionController>();

        private readonly Guid _principalId = Guid.NewGuid();
        private readonly Project _defaultProject = Project.Example();
        private readonly GuestVerificationRequest _defaultGuestVerificationRequest;

        public GuestSessionModuleTests()
        {
            TenantId = Guid.NewGuid();
            var defaultGuestUser = User.GuestUserExample();
            defaultGuestUser.Id = _principalId;
            defaultGuestUser.IsLocked = false;
            defaultGuestUser.IsEmailVerified = true;
            defaultGuestUser.EmailVerifiedAt = DateTime.Now.AddDays(-1.0);

            _defaultProject.TenantId = TenantId;

            _defaultGuestVerificationRequest = new GuestVerificationRequest() { ProjectAccessCode = _defaultProject.GuestAccessCode, ProjectId = _defaultProject.Id, Username = defaultGuestUser.Username };
        }

        protected override List<object> BrowserDependencies => new List<object> { _guestSessionControllerMock.Object };

        [Fact]
        public async Task VerifyGuestAsync_CallsVerifyGuestAsyncControllerMethodWithExpectedArgs()
        {
            await UserTokenBrowser.Post($"{Routing.VerifyGuestRoute}", ctx => BuildRequest(ctx, _defaultGuestVerificationRequest));

            _guestSessionControllerMock
                .Verify(x => x.VerifyGuestAsync(It.Is<GuestVerificationRequest>(gvr =>
                gvr.ProjectAccessCode == _defaultGuestVerificationRequest.ProjectAccessCode &&
                gvr.ProjectId == _defaultGuestVerificationRequest.ProjectId &&
                gvr.Username == _defaultGuestVerificationRequest.Username), It.Is<Guid>(t => t == TenantId)));
        }

        [Fact]
        public async Task VerifyGuestAsync_WhenHappyPath_ReturnsOk()
        {
            var response = await UserTokenBrowser.Post($"{Routing.VerifyGuestRoute}", ctx => BuildRequest(ctx, _defaultGuestVerificationRequest));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task VerifyGuestAsync_WhenValidationFails_ReturnsBadRequestValidationFailedException()
        {
            _guestSessionControllerMock
                .Setup(x => x.VerifyGuestAsync(It.IsAny<GuestVerificationRequest>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure> { _expectedValidationFailure }));

            var response = await UserTokenBrowser.Post($"{Routing.VerifyGuestRoute}", ctx => BuildRequest(ctx, _defaultGuestVerificationRequest));

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

            var response = await UserTokenBrowser.Post($"{Routing.VerifyGuestRoute}", ctx => BuildRequest(ctx, _defaultGuestVerificationRequest));

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

            var response = await UserTokenBrowser.Get($"{Routing.GuestSessionsRoute}/{Guid.NewGuid()}", BuildRequest);

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

            var response = await UserTokenBrowser.Get($"{Routing.GuestSessionsRoute}/{Guid.NewGuid()}", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestSession_ReturnsOk()
        {
            _guestSessionControllerMock
                .Setup(x => x.GetGuestSessionAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new GuestSession());

            var response = await UserTokenBrowser.Get($"{Routing.GuestSessionsRoute}/{Guid.NewGuid()}", BuildRequest);

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

            var response = await UserTokenBrowser.Put($"{Routing.GuestSessionsRoute}/{_guestSession.Id}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task UpdateGuestSession_OnUnexpectedException_ReturnsInternalServerError()
        {
            _guestSessionControllerMock
                .Setup(x => x.UpdateGuestSessionAsync(It.IsAny<GuestSession>(), It.IsAny<Guid>()))
                .Throws<Exception>();

            var response = await UserTokenBrowser.Put($"{Routing.GuestSessionsRoute}/{_guestSession.Id}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task EmailHost_ReturnsOk()
        {
            _guestSessionControllerMock
                .Setup(x => x.EmailHostAsync(It.IsAny<string>(), It.IsAny<Guid>()))
                .ReturnsAsync(new SendHostEmailResponse());

            var response = await UserTokenBrowser.Get($"{Routing.GuestSessionsRoute}/{Routing.ProjectsPath}/{_guestSession.ProjectAccessCode}/{Routing.EmailHostPath}", BuildRequest);

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

            var response = await UserTokenBrowser.Get($"{Routing.GuestSessionsRoute}/{Routing.ProjectsPath}/{_guestSession.ProjectAccessCode}/{Routing.EmailHostPath}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
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

            var response = await UserTokenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.UsersPath}/{Routing.GuestSessionsPath}", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestSessionsByProjectIdForCurrentUser_WhenValidationFails_ReturnsBadRequestValidationFailedException()
        {
            _guestSessionControllerMock
                .Setup(x => x.GetValidGuestSessionsByProjectIdForCurrentUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure> { _expectedValidationFailure }));

            var response = await UserTokenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.UsersPath}/{Routing.GuestSessionsPath}",  BuildRequest);

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

            var response = await UserTokenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.UsersPath}/{Routing.GuestSessionsPath}", BuildRequest);

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

            var response = await UserTokenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.UsersPath}/{Guid.NewGuid()}/{Routing.GuestSessionsPath}", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestSessionsByProjectIdForUser_WhenValidationFails_ReturnsBadRequestValidationFailedException()
        {
            _guestSessionControllerMock
                .Setup(x => x.GetGuestSessionsByProjectIdForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure> { _expectedValidationFailure }));

            var response = await UserTokenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.UsersPath}/{Guid.NewGuid()}/{Routing.GuestSessionsPath}", BuildRequest);

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

            var response = await UserTokenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.UsersPath}/{Guid.NewGuid()}/{Routing.GuestSessionsPath}", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
