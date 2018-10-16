using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentValidation.Results;
using Moq;
using Nancy;
using Nancy.Testing;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.InternalApi.Constants;
using Synthesis.GuestService.Exceptions;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Entity;
using Synthesis.Nancy.MicroService.Validation;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Modules
{
    public class GuestInviteModuleTests : BaseModuleTests<GuestInviteModule>
    {
        private readonly ValidationFailure _expectedValidationFailure = new ValidationFailure("theprop", "thereason");
        private readonly GuestInvite _guestInvite = new GuestInvite { Id = Guid.NewGuid(), InvitedBy = Guid.NewGuid(), ProjectId = Guid.NewGuid(), CreatedDateTime = DateTime.UtcNow };
        private readonly Mock<IGuestInviteController> _guestInviteControllerMock = new Mock<IGuestInviteController>();

        protected override List<object> BrowserDependencies => new List<object>() { _guestInviteControllerMock.Object };

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

            var response = await UserTokenBrowser.Get($"{Routing.GuestInvitesRoute}/{Guid.NewGuid()}", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestInviteByIdReturnsInternalServerErrorOnUnexpectedException()
        {
            _guestInviteControllerMock
                .Setup(x => x.GetGuestInviteAsync(It.IsAny<Guid>()))
                .Throws<Exception>();

            var response = await UserTokenBrowser.Get($"{Routing.GuestInvitesRoute}/{Guid.NewGuid()}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestInviteByIdReturnsBadRequestValidationFailedException()
        {
            _guestInviteControllerMock
                .Setup(x => x.GetGuestInviteAsync(It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure> { _expectedValidationFailure }));

            var response = await UserTokenBrowser.Get($"{Routing.GuestInvitesRoute}/{Guid.NewGuid()}", ctx => BuildRequest(ctx, _guestInvite));

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

            var response = await UserTokenBrowser.Get($"{Routing.GuestInvitesRoute}/{Guid.NewGuid()}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task CreateGuestInviteReturnsOk()
        {
            var response = await UserTokenBrowser.Post($"{Routing.GuestInvitesRoute}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CreateGuestInviteReturnsInternalServerErrorOnUnexpectedException()
        {
            _guestInviteControllerMock
                .Setup(x => x.CreateGuestInviteAsync(It.IsAny<GuestInvite>()))
                .Throws<Exception>();

            var response = await UserTokenBrowser.Post($"{Routing.GuestInvitesRoute}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        
        [Fact]
        public async Task CreateGuestInviteReturnsInternalServerErrorOnGetProjectException()
        {
            _guestInviteControllerMock
                .Setup(x => x.CreateGuestInviteAsync(It.IsAny<GuestInvite>()))
                .Throws<GetProjectException>();

            var response = await UserTokenBrowser.Post($"{Routing.GuestInvitesRoute}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CreateGuestInviteReturnsInternalServerErrorOnGetUserException()
        {
            _guestInviteControllerMock
                .Setup(x => x.CreateGuestInviteAsync(It.IsAny<GuestInvite>()))
                .Throws<GetUserException>();

            var response = await UserTokenBrowser.Post($"{Routing.GuestInvitesRoute}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CreateGuestInviteReturnsInternalServerErrorOnGetAccessCodeException()
        {
            _guestInviteControllerMock
                .Setup(x => x.CreateGuestInviteAsync(It.IsAny<GuestInvite>()))
                .Throws<ResetAccessCodeException>();

            var response = await UserTokenBrowser.Post($"{Routing.GuestInvitesRoute}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CreateGuestInviteReturnsBadRequestValidationFailedException()
        {
            _guestInviteControllerMock
                .Setup(x => x.CreateGuestInviteAsync(It.IsAny<GuestInvite>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure> { _expectedValidationFailure }));

            var response = await UserTokenBrowser.Post($"{Routing.GuestInvitesRoute}", ctx => BuildRequest(ctx, _guestInvite));

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

            var response = await UserTokenBrowser.Put($"{Routing.GuestInvitesRoute}/{_guestInvite.Id}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task UpdateGuestInviteReturnsInternalServerErrorOnUnexpectedException()
        {
            _guestInviteControllerMock
                .Setup(x => x.UpdateGuestInviteAsync(It.IsAny<GuestInvite>()))
                .Throws<Exception>();

            var response = await UserTokenBrowser.Put($"{Routing.GuestInvitesRoute}/{_guestInvite.Id}", ctx => BuildRequest(ctx, _guestInvite));

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
        public async Task GetGuestInvitesByUserIdAsyncWithoutAccessReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.GuestInvitesPath}", BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestInviteByUserIdReturnsOk()
        {
            _guestInviteControllerMock
                .Setup(x => x.GetGuestInvitesForUserAsync(It.IsAny<GetGuestInvitesRequest>()))
                .ReturnsAsync(new List<GuestInvite> { GuestInvite.Example() });

            var response = await UserTokenBrowser.Post($"{Routing.UsersRoute}/{Routing.GuestInvitesPath}", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestInviteByUserIdReturnsInternalServerErrorOnUnexpectedException()
        {
            _guestInviteControllerMock
                .Setup(x => x.GetGuestInvitesForUserAsync(It.IsAny<GetGuestInvitesRequest>()))
                .Throws<Exception>();

            var response = await UserTokenBrowser.Post($"{Routing.UsersRoute}/{Routing.GuestInvitesPath}", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestInviteByUserIdReturnsBadRequestOnValidationFailedException()
        {
            _guestInviteControllerMock
                .Setup(x => x.GetGuestInvitesForUserAsync(It.IsAny<GetGuestInvitesRequest>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await UserTokenBrowser.Post($"{Routing.UsersRoute}/{Routing.GuestInvitesPath}", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UpdateGuestInviteAsyncWithoutAccessReturnsForbidden()
        {
            var response = await ForbiddenBrowser.Put($"{Routing.GuestInvitesRoute}/{Guid.NewGuid()}", BuildRequest);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestInviteByProjectIdReturnsOk()
        {
            _guestInviteControllerMock
                .Setup(x => x.GetValidGuestInvitesByProjectIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new List<GuestInvite> { GuestInvite.Example() });

            var response = await UserTokenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.GuestInvitesPath}", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestInviteByProjectIdReturnsInternalServerErrorOnUnexpectedException()
        {
            _guestInviteControllerMock
                .Setup(x => x.GetValidGuestInvitesByProjectIdAsync(It.IsAny<Guid>()))
                .Throws<Exception>();

            var response = await UserTokenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.GuestInvitesPath}", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestInviteByProjectIdReturnsBadRequestOnValidationFailedException()
        {
            _guestInviteControllerMock
                .Setup(x => x.GetValidGuestInvitesByProjectIdAsync(It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await UserTokenBrowser.Get($"{Routing.ProjectsRoute}/{Guid.NewGuid()}/{Routing.GuestInvitesPath}", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
