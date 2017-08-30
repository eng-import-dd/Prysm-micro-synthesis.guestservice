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
using Synthesis.GuestService.Validators;
using Synthesis.GuestService.Workflow.Interfaces;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Validation;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Synthesis.Nancy.MicroService.Entity;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Modules
{
    public class GuestSessionModuleTests
    {
        private readonly Mock<IGuestSessionController> _guestSessionControllerMock = new Mock<IGuestSessionController>();
        private readonly Mock<IMetadataRegistry> _metadataRegistryMock = new Mock<IMetadataRegistry>();
        private readonly Mock<ILogger> _loggerMock = new Mock<ILogger>();

        private readonly Browser _browser;
        private readonly GuestSession _guestSession = new GuestSession { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ProjectId = Guid.NewGuid(), ProjectAccessCode = "12345" };
        private readonly ValidationFailure _expectedValidationFailure = new ValidationFailure("theprop", "thereason");

        public GuestSessionModuleTests()
        {
            _browser = BrowserWithRequestStartup((container, pipelines, context) =>
            {
                context.CurrentUser = new ClaimsPrincipal(
                    new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Name, "TestUser"),
                        new Claim(ClaimTypes.Email, "test@user.com")
                    },
                    AuthenticationTypes.Basic));
            });
        }

        #region GET Route Tests
        [Theory]
        [InlineData(BaseRoutes.GuestSession)]
        [InlineData(BaseRoutes.GuestSessionLegacy)]
        public async Task GetGuestSessionByIdReturnsOk(string route)
        {
            _guestSessionControllerMock
               .Setup(x => x.GetGuestSessionAsync(It.IsAny<Guid>()))
               .ReturnsAsync(new GuestSession());

            var response = await _browser.Get($"{route}/{Guid.NewGuid()}", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData(BaseRoutes.GuestSession)]
        [InlineData(BaseRoutes.GuestSessionLegacy)]
        public async Task GetGuestSessionByIdReturnsInternalServerErrorOnUnexpectedException(string route)
        {
            _guestSessionControllerMock
                .Setup(x => x.GetGuestSessionAsync(It.IsAny<Guid>()))
                .Throws<Exception>();

            var response = await _browser.Get($"{route}/{Guid.NewGuid()}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Theory]
        [InlineData(BaseRoutes.GuestSession)]
        [InlineData(BaseRoutes.GuestSessionLegacy)]
        public async Task GetGuestSessionByIdReturnsNotFoundException(string route)
        {
            _guestSessionControllerMock
                .Setup(x => x.GetGuestSessionAsync(It.IsAny<Guid>()))
                .Throws(new NotFoundException("GuestSession not found"));

            var response = await _browser.Get($"{route}/project/{Guid.NewGuid()}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Theory]
        [InlineData(BaseRoutes.GuestSession)]
        [InlineData(BaseRoutes.GuestSessionLegacy)]
        public async Task GetGuestSessionByIdReturnsBadRequestValidationFailedException(string route)
        {
            _guestSessionControllerMock
                .Setup(x => x.GetGuestSessionAsync(It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure> { _expectedValidationFailure }));

            var response = await _browser.Get($"{route}/{Guid.NewGuid()}", ctx => BuildRequest(ctx, _guestSession));

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
        #endregion

        #region CREATE Route Tests
        [Theory]
        [InlineData(BaseRoutes.GuestSession)]
        [InlineData(BaseRoutes.GuestSessionLegacy)]
        public async Task CreateGuestSessionReturnsOk(string route)
        {
            var response = await _browser.Post($"{route}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData(BaseRoutes.GuestSession)]
        [InlineData(BaseRoutes.GuestSessionLegacy)]
        public async Task CreateGuestSessionReturnsInternalServerErrorOnUnexpectedException(string route)
        {
            _guestSessionControllerMock
                .Setup(x => x.CreateGuestSessionAsync(It.IsAny<GuestSession>()))
                .Throws<Exception>();

            var response = await _browser.Post($"{route}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Theory]
        [InlineData(BaseRoutes.GuestSession)]
        [InlineData(BaseRoutes.GuestSessionLegacy)]
        public async Task CreateGuestSessionReturnsBadRequestBindingException(string route)
        {
            _guestSessionControllerMock
                .Setup(x => x.CreateGuestSessionAsync(_guestSession))
                .Throws<Exception>();

            var response = await _browser.Post($"{route}", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseMessages.FailedToBind, response.ReasonPhrase);
        }

        [Theory]
        [InlineData(BaseRoutes.GuestSession)]
        [InlineData(BaseRoutes.GuestSessionLegacy)]
        public async Task CreateGuestSessionReturnsBadRequestValidationFailedException(string route)
        {
            _guestSessionControllerMock
                .Setup(x => x.CreateGuestSessionAsync(It.IsAny<GuestSession>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure> { _expectedValidationFailure }));

            var response = await _browser.Post($"{route}", ctx => BuildRequest(ctx, _guestSession));

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
        #endregion

        #region UPDATE Route Tests
        [Theory]
        [InlineData(BaseRoutes.GuestSession)]
        [InlineData(BaseRoutes.GuestSessionLegacy)]
        public async Task UpdateGuestSessionReturnsOk(string route)
        {
            _guestSessionControllerMock
                .Setup(x => x.UpdateGuestSessionAsync(_guestSession.Id, It.IsAny<GuestSession>()))
                .ReturnsAsync(new GuestSession());

            var response = await _browser.Put($"{route}/{_guestSession.Id}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData(BaseRoutes.GuestSession)]
        [InlineData(BaseRoutes.GuestSessionLegacy)]
        public async Task UpdateGuestSessionReturnsInternalServerErrorOnUnexpectedException(string route)
        {
            _guestSessionControllerMock
                .Setup(x => x.UpdateGuestSessionAsync(_guestSession.Id, It.IsAny<GuestSession>()))
                .Throws<Exception>();

            var response = await _browser.Put($"{route}/{_guestSession.Id}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Theory]
        [InlineData(BaseRoutes.GuestSession)]
        [InlineData(BaseRoutes.GuestSessionLegacy)]
        public async Task UpdateGuestSessionReturnsBadRequestBindingException(string route)
        {
            _guestSessionControllerMock
                .Setup(x => x.UpdateGuestSessionAsync(_guestSession.Id, It.IsAny<GuestSession>()))
                .Throws<Exception>();

            var response = await _browser.Put($"{route}/{_guestSession.Id}", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseMessages.FailedToBind, response.ReasonPhrase);
        }
        #endregion

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
                var resource = new GuestSession
                {
                    Id = Guid.Parse("2c1156fa-5902-4978-9c3d-ebcb77ae0d55"),
                    CreatedDateTime = DateTime.UtcNow,
                    LastAccessDate = DateTime.UtcNow
                };
                var repositoryMock = new Mock<IRepository<GuestSession>>();
                repositoryMock
                    .Setup(r => r.GetItemAsync(It.IsAny<Guid>()))
                    .ReturnsAsync(resource);

                var repositoryFactoryMock = new Mock<IRepositoryFactory>();
                repositoryFactoryMock
                    .Setup(f => f.CreateRepository<GuestSession>())
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
                with.Dependency(_guestSessionControllerMock.Object);
                with.Dependency(validatorLocatorMock.Object);
                with.Dependency(repositoryFactoryMock.Object);
                with.Dependency(eventServiceMock.Object);
                with.Module<GuestSessionModule>();
            });
        }

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
    }
}
