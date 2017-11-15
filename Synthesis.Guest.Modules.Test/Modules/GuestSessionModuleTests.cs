﻿using System;
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
using Synthesis.Authentication;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.Models;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Entity;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PolicyEvaluator;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Modules
{
    public class GuestSessionModuleTests
    {
        private readonly Browser _browserAuth;
        private readonly Browser _browserNoAuth;
        private readonly ValidationFailure _expectedValidationFailure = new ValidationFailure("theprop", "thereason");
        private readonly GuestSession _guestSession = new GuestSession { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ProjectId = Guid.NewGuid(), ProjectAccessCode = "12345" };
        private readonly Mock<IGuestSessionController> _guestSessionControllerMock = new Mock<IGuestSessionController>();

        public GuestSessionModuleTests()
        {
            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock
                .Setup(x => x.Get(It.IsAny<LogTopic>()))
                .Returns(new Mock<ILogger>().Object);

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
                with.Dependency(new Mock<ITokenValidator>().Object);
                with.Dependency(new Mock<IPolicyEvaluator>().Object);
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

        [Fact]
        public async Task GetGuestSessionReturnsUnauthorizedRequest()
        {
            _guestSessionControllerMock
                .Setup(x => x.GetGuestSessionAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new GuestSession());

            var response = await _browserNoAuth.Get($"{Routing.GuestSessionsRoute}/{Guid.NewGuid()}", BuildRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task CreateGuestSessionReturnsUnauthorizedRequest()
        {
            var response = await _browserNoAuth.Post($"{Routing.GuestSessionsRoute}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task UpdateGuestSessionReturnsUnauthorizedRequest()
        {
            _guestSessionControllerMock
                .Setup(x => x.UpdateGuestSessionAsync(It.IsAny<GuestSession>()))
                .ReturnsAsync(new GuestSession());
            var response = await _browserNoAuth.Put($"{Routing.GuestSessionsRoute}/{_guestSession.Id}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestSessionByIdReturnsOk()
        {
            _guestSessionControllerMock
                .Setup(x => x.GetGuestSessionAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new GuestSession());

            var response = await _browserAuth.Get($"{Routing.GuestSessionsRoute}/{Guid.NewGuid()}", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestSessionByIdReturnsInternalServerErrorOnUnexpectedException()
        {
            _guestSessionControllerMock
                .Setup(x => x.GetGuestSessionAsync(It.IsAny<Guid>()))
                .Throws<Exception>();

            var response = await _browserAuth.Get($"{Routing.GuestSessionsRoute}/{Guid.NewGuid()}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestSessionByIdReturnsBadRequestValidationFailedException()
        {
            _guestSessionControllerMock
                .Setup(x => x.GetGuestSessionAsync(It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure> { _expectedValidationFailure }));

            var response = await _browserAuth.Get($"{Routing.GuestSessionsRoute}/{Guid.NewGuid()}", ctx => BuildRequest(ctx, _guestSession));

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
            var response = await _browserAuth.Post($"{Routing.GuestSessionsRoute}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CreateGuestSessionReturnsInternalServerErrorOnUnexpectedException()
        {
            _guestSessionControllerMock
                .Setup(x => x.CreateGuestSessionAsync(It.IsAny<GuestSession>()))
                .Throws<Exception>();

            var response = await _browserAuth.Post($"{Routing.GuestSessionsRoute}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CreateGuestSessionReturnsBadRequestValidationFailedException()
        {
            _guestSessionControllerMock
                .Setup(x => x.CreateGuestSessionAsync(It.IsAny<GuestSession>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure> { _expectedValidationFailure }));

            var response = await _browserAuth.Post($"{Routing.GuestSessionsRoute}", ctx => BuildRequest(ctx, _guestSession));

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

            var response = await _browserAuth.Put($"{Routing.GuestSessionsRoute}/{_guestSession.Id}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task UpdateGuestSessionReturnsInternalServerErrorOnUnexpectedException()
        {
            _guestSessionControllerMock
                .Setup(x => x.UpdateGuestSessionAsync(It.IsAny<GuestSession>()))
                .Throws<Exception>();

            var response = await _browserAuth.Put($"{Routing.GuestSessionsRoute}/{_guestSession.Id}", ctx => BuildRequest(ctx, _guestSession));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
    }
}