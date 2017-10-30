using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.Testing;
using Nancy.TinyIoc;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Dao.Models;
using Synthesis.GuestService.Workflow.Controllers;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Entity;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Validation;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Modules
{
    public class GuestInviteModuleTests
    {
        private readonly Browser _browserAuth;
        private readonly Browser _browserNoAuth;
        private readonly ValidationFailure _expectedValidationFailure = new ValidationFailure("theprop", "thereason");
        private readonly GuestInvite _guestInvite = new GuestInvite { Id = Guid.NewGuid(), InvitedBy = Guid.NewGuid(), ProjectId = Guid.NewGuid(), CreatedDateTime = DateTime.UtcNow };
        private readonly Mock<IGuestInviteController> _guestInviteControllerMock = new Mock<IGuestInviteController>();
        private readonly Mock<ILogger> _loggerMock = new Mock<ILogger>();
        private readonly Mock<IMetadataRegistry> _metadataRegistryMock = new Mock<IMetadataRegistry>();

        public GuestInviteModuleTests()
        {
            _browserAuth = BrowserWithRequestStartup((container, pipelines, context) =>
            {
                context.CurrentUser = new ClaimsPrincipal(
                            new ClaimsIdentity(new[]
                            {
                                new Claim(ClaimTypes.Name, "TestUser"),
                                new Claim(ClaimTypes.Email, "test@user.com")
                            },
                            AuthenticationTypes.Basic));
            });

            _browserNoAuth = BrowserWithRequestStartup((container, pipelines, context) => { });
        }

        private Browser BrowserWithRequestStartup(Action<TinyIoCContainer, IPipelines, NancyContext> requestStartup)
        {
            return new Browser(with =>
            {
                var mockLogger = new Mock<ILogger>();

                mockLogger.Setup(l => l.LogMessage(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>())).Callback(() => Console.Write(""));
                var logger = mockLogger.Object;
                var loggerFactoryMock = new Mock<ILoggerFactory>();
                loggerFactoryMock.Setup(f => f.Get(It.IsAny<LogTopic>())).Returns(logger);

                var loggerFactory = loggerFactoryMock.Object;
                var resource = new GuestInvite
                {
                    Id = Guid.Parse("2c1156fa-5902-4978-9c3d-ebcb77ae0d55"),
                    CreatedDateTime = DateTime.UtcNow,
                    LastAccessDate = DateTime.UtcNow
                };
                var repositoryMock = new Mock<IRepository<GuestInvite>>();
                repositoryMock
                    .Setup(r => r.GetItemAsync(It.IsAny<Guid>()))
                    .ReturnsAsync(resource);

                var repositoryFactoryMock = new Mock<IRepositoryFactory>();
                repositoryFactoryMock
                    .Setup(f => f.CreateRepository<GuestInvite>())
                    .Returns(repositoryMock.Object);

                var eventServiceMock = new Mock<IEventService>();
                eventServiceMock.Setup(s => s.PublishAsync(It.IsAny<string>()));

                var validatorMock = new Mock<IValidator>();
                validatorMock
                    .Setup(v => v.ValidateAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ValidationResult());
                var validatorLocatorMock = new Mock<IValidatorLocator>();
                validatorLocatorMock
                    .Setup(l => l.GetValidator(It.IsAny<Type>()))
                    .Returns(validatorMock.Object);

                with.EnableAutoRegistration();
                with.RequestStartup(requestStartup);
                with.Dependency(new Mock<IMetadataRegistry>().Object);
                with.Dependency(loggerFactory);
                with.Dependency(logger);
                with.Dependency(_guestInviteControllerMock.Object);
                with.Dependency(validatorLocatorMock.Object);
                with.Dependency(repositoryFactoryMock.Object);
                with.Dependency(eventServiceMock.Object);
                with.Module<GuestInviteModule>();
            });
        }

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

        [Theory]
        [InlineData(BaseRoutes.GuestInvite)]
        [InlineData(BaseRoutes.GuestInviteLegacy)]
        public async Task GetGuestInviteReturnsUnauthorizedRequest(string route)
        {
            _guestInviteControllerMock
                .Setup(x => x.GetGuestInviteAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new GuestInvite());

            var response = await _browserNoAuth.Get($"{route}/{Guid.NewGuid()}", BuildRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData(BaseRoutes.GuestInvite)]
        [InlineData(BaseRoutes.GuestInviteLegacy)]
        public async Task CreateGuestInviteReturnsUnauthorizedRequest(string route)
        {
            var response = await _browserNoAuth.Post($"{route}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData(BaseRoutes.GuestInvite)]
        [InlineData(BaseRoutes.GuestInviteLegacy)]
        public async Task UpdateGuestInviteReturnsUnauthorizedRequest(string route)
        {
            _guestInviteControllerMock
                .Setup(x => x.UpdateGuestInviteAsync(It.IsAny<GuestInvite>()))
                .ReturnsAsync(new GuestInvite());

            var response = await _browserNoAuth.Put($"{route}/{_guestInvite.Id}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Theory]
        [InlineData(BaseRoutes.GuestInvite)]
        [InlineData(BaseRoutes.GuestInviteLegacy)]
        public async Task GetGuestInviteByIdReturnsOk(string route)
        {
            _guestInviteControllerMock
                .Setup(x => x.GetGuestInviteAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new GuestInvite());

            var response = await _browserAuth.Get($"{route}/{Guid.NewGuid()}", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData(BaseRoutes.GuestInvite)]
        [InlineData(BaseRoutes.GuestInviteLegacy)]
        public async Task GetGuestInviteByIdReturnsInternalServerErrorOnUnexpectedException(string route)
        {
            _guestInviteControllerMock
                .Setup(x => x.GetGuestInviteAsync(It.IsAny<Guid>()))
                .Throws<Exception>();

            var response = await _browserAuth.Get($"{route}/{Guid.NewGuid()}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Theory]
        [InlineData(BaseRoutes.GuestInvite)]
        [InlineData(BaseRoutes.GuestInviteLegacy)]
        public async Task GetGuestInviteByIdReturnsBadRequestValidationFailedException(string route)
        {
            _guestInviteControllerMock
                .Setup(x => x.GetGuestInviteAsync(It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure> { _expectedValidationFailure }));

            var response = await _browserAuth.Get($"{route}/{Guid.NewGuid()}", ctx => BuildRequest(ctx, _guestInvite));

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

        [Theory]
        [InlineData(BaseRoutes.GuestInvite)]
        [InlineData(BaseRoutes.GuestInviteLegacy)]
        public async Task GetGuestInviteByIdReturnsNotFoundException(string route)
        {
            _guestInviteControllerMock
                .Setup(x => x.GetGuestInviteAsync(It.IsAny<Guid>()))
                .Throws(new NotFoundException("GuestInvite not found"));

            var response = await _browserAuth.Get($"{route}/{Guid.NewGuid()}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Theory]
        [InlineData(BaseRoutes.GuestInvite)]
        [InlineData(BaseRoutes.GuestInviteLegacy)]
        public async Task CreateGuestInviteReturnsOk(string route)
        {
            var response = await _browserAuth.Post($"{route}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData(BaseRoutes.GuestInvite)]
        [InlineData(BaseRoutes.GuestInviteLegacy)]
        public async Task CreateGuestInviteReturnsInternalServerErrorOnUnexpectedException(string route)
        {
            _guestInviteControllerMock
                .Setup(x => x.CreateGuestInviteAsync(It.IsAny<GuestInvite>()))
                .Throws<Exception>();

            var response = await _browserAuth.Post($"{route}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Theory]
        [InlineData(BaseRoutes.GuestInvite)]
        [InlineData(BaseRoutes.GuestInviteLegacy)]
        public async Task CreateGuestInviteReturnsBadRequestBindingException(string route)
        {
            _guestInviteControllerMock
                .Setup(x => x.CreateGuestInviteAsync(_guestInvite))
                .Throws<Exception>();

            var response = await _browserAuth.Post($"{route}", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseMessages.FailedToBind, response.ReasonPhrase);
        }

        [Theory]
        [InlineData(BaseRoutes.GuestInvite)]
        [InlineData(BaseRoutes.GuestInviteLegacy)]
        public async Task CreateGuestInviteReturnsBadRequestValidationFailedException(string route)
        {
            _guestInviteControllerMock
                .Setup(x => x.CreateGuestInviteAsync(It.IsAny<GuestInvite>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure> { _expectedValidationFailure }));

            var response = await _browserAuth.Post($"{route}", ctx => BuildRequest(ctx, _guestInvite));

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

        [Theory]
        [InlineData(BaseRoutes.GuestInvite)]
        [InlineData(BaseRoutes.GuestInviteLegacy)]
        public async Task UpdateGuestInviteReturnsOk(string route)
        {
            _guestInviteControllerMock
                .Setup(x => x.UpdateGuestInviteAsync(It.IsAny<GuestInvite>()))
                .ReturnsAsync(new GuestInvite());

            var response = await _browserAuth.Put($"{route}/{_guestInvite.Id}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData(BaseRoutes.GuestInvite)]
        [InlineData(BaseRoutes.GuestInviteLegacy)]
        public async Task UpdateGuestInviteReturnsInternalServerErrorOnUnexpectedException(string route)
        {
            _guestInviteControllerMock
                .Setup(x => x.UpdateGuestInviteAsync(It.IsAny<GuestInvite>()))
                .Throws<Exception>();

            var response = await _browserAuth.Put($"{route}/{_guestInvite.Id}", ctx => BuildRequest(ctx, _guestInvite));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Theory]
        [InlineData(BaseRoutes.GuestInvite)]
        [InlineData(BaseRoutes.GuestInviteLegacy)]
        public async Task UpdateGuestInviteReturnsBadRequestBindingException(string route)
        {
            _guestInviteControllerMock
                .Setup(x => x.UpdateGuestInviteAsync(It.IsAny<GuestInvite>()))
                .Throws<Exception>();

            var response = await _browserAuth.Put($"{route}/{_guestInvite.Id}", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseMessages.FailedToBind, response.ReasonPhrase);
        }
    }
}