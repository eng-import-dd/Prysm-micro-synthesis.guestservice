using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.ApiWrappers.Interfaces;
using Synthesis.GuestService.InternalApi.Constants;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.InternalApi.Enums;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.Utilities.Interfaces;
using Synthesis.Http.Microservice;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.ParticipantService.InternalApi.Services;
using Synthesis.PrincipalService.InternalApi.Api;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.ProjectService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Models;
using Synthesis.Serialization;
using Synthesis.SettingService.InternalApi.Api;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Controllers
{
    public class GuestSessionControllerTests
    {
        public GuestSessionControllerTests()
        {
            _defaultGuestSession.Id = Guid.NewGuid();
            _defaultGuestSession.UserId = Guid.NewGuid();
            _defaultGuestSession.ProjectId = Guid.NewGuid();
            _defaultGuestSession.ProjectAccessCode = "0123456789";

            var repositoryFactoryMock = new Mock<IRepositoryFactory>();
            _guestSessionRepositoryMock = new Mock<IRepository<GuestSession>>();
            var guestInviteRepositoryMock = new Mock<IRepository<GuestInvite>>();

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_defaultGuestSession);

            _guestSessionRepositoryMock
                .Setup(x => x.CreateItemAsync(It.IsAny<GuestSession>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((GuestSession participant, CancellationToken c) => participant);

            _guestSessionRepositoryMock
                .Setup(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestSession>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, GuestSession participant, UpdateOptions o, CancellationToken c) => participant);

            guestInviteRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_defaultGuestInvite);

            guestInviteRepositoryMock
                .Setup(x => x.CreateItemAsync(It.IsAny<GuestInvite>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((GuestInvite session, CancellationToken c) => session);

            guestInviteRepositoryMock
                .Setup(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestInvite>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, GuestInvite session, UpdateOptions o, CancellationToken c) => session);

            repositoryFactoryMock
                .Setup(x => x.CreateRepository<GuestSession>())
                .Returns(_guestSessionRepositoryMock.Object);

            _validatorMock
                .Setup(v => v.ValidateAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());

            _validatorMock
                .Setup(v => v.Validate(It.IsAny<object>()))
                .Returns(new ValidationResult());

            _validatorLocator
                .Setup(g => g.GetValidator(It.IsAny<Type>()))
                .Returns(_validatorMock.Object);

            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock
                .Setup(x => x.Get(It.IsAny<LogTopic>()))
                .Returns(new Mock<ILogger>().Object);

            _target = new GuestSessionController(repositoryFactoryMock.Object, _validatorLocator.Object, _eventServiceMock.Object,
                                                 loggerFactoryMock.Object, _emailUtility.Object, _projectApiMock.Object, _serviceToServiceProjectApiMock.Object,
                                                 _userApiMock.Object, _projectLobbyStateController.Object, _settingsApiMock.Object, _sessionService.Object, _synthesisObjectSerializer.Object);
        }

        private readonly GuestSessionController _target;
        private readonly Mock<IRepository<GuestSession>> _guestSessionRepositoryMock;
        private readonly Mock<IEventService> _eventServiceMock = new Mock<IEventService>();
        private readonly Mock<IEmailUtility> _emailUtility = new Mock<IEmailUtility>();
        private readonly Mock<IProjectApi> _projectApiMock = new Mock<IProjectApi>();
        private readonly Mock<IProjectApi> _serviceToServiceProjectApiMock = new Mock<IProjectApi>();
        private readonly Mock<ISettingApi> _settingsApiMock = new Mock<ISettingApi>();
        private readonly Mock<IUserApi> _userApiMock = new Mock<IUserApi>();
        private readonly GuestSession _defaultGuestSession = new GuestSession();
        private readonly GuestInvite _defaultGuestInvite = new GuestInvite();
        private readonly Mock<IValidator> _validatorMock = new Mock<IValidator>();
        private readonly Mock<IValidatorLocator> _validatorLocator = new Mock<IValidatorLocator>();
        private readonly Mock<IProjectLobbyStateController> _projectLobbyStateController = new Mock<IProjectLobbyStateController>();
        private readonly Mock<ISessionService> _sessionService = new Mock<ISessionService>();
        private readonly Mock<IObjectSerializer> _synthesisObjectSerializer = new Mock<Synthesis.Serialization.IObjectSerializer>();

        private static ValidationResult FailedValidationResult => new ValidationResult(
            new List<ValidationFailure>
            {
                new ValidationFailure(string.Empty, string.Empty)
            }
        );

        [Fact]
        public async Task CreateGuestSessionCallsCreate()
        {
            await _target.CreateGuestSessionAsync(_defaultGuestSession);
            _guestSessionRepositoryMock.Verify(x => x.CreateItemAsync(It.IsAny<GuestSession>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task CreateGuestSessionCallsDeleteItemsToClearOldGuestSessionsForUserAndProject()
        {
            await _target.CreateGuestSessionAsync(_defaultGuestSession);

            _guestSessionRepositoryMock.Verify(x => x.DeleteItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task CreateGuestSessionReturnsProvidedGuestSession()
        {
            var result = await _target.CreateGuestSessionAsync(_defaultGuestSession);
            Assert.NotNull(result);
            Assert.Equal(_defaultGuestSession.Id, result.Id);
            Assert.Equal(_defaultGuestSession.UserId, result.UserId);
            Assert.Equal(_defaultGuestSession.ProjectId, result.ProjectId);
            Assert.Equal(_defaultGuestSession.ProjectAccessCode, result.ProjectAccessCode);
        }

        [Fact]
        public async Task CreateGuestSessionVerifyCalled()
        {
            await _target.CreateGuestSessionAsync(_defaultGuestSession);
            _guestSessionRepositoryMock.Verify(x => x.CreateItemAsync(It.IsAny<GuestSession>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task CreateNewGuestSessionBussesEvent()
        {
            await _target.CreateGuestSessionAsync(_defaultGuestSession);
            _eventServiceMock.Verify(x => x.PublishAsync(It.IsAny<ServiceBusEvent<GuestSession>>()));
        }

        [Fact]
        public async Task CreateNewGuestSessionSetsProjectAccessCode()
        {
            var result = await _target.CreateGuestSessionAsync(_defaultGuestSession);
            Assert.NotNull(result);
            Assert.NotEqual(string.Empty, result.ProjectAccessCode);
            Assert.Equal(_defaultGuestSession.ProjectAccessCode, result.ProjectAccessCode);
        }

        [Fact]
        public async Task CreateNewGuestSessionSetsProjectId()
        {
            var result = await _target.CreateGuestSessionAsync(_defaultGuestSession);
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.ProjectId);
            Assert.Equal(_defaultGuestSession.ProjectId, result.ProjectId);
        }

        [Fact]
        public async Task CreateGuestSessionThrowsWhenUpdatingExistingGuestSessionsThrows()
        {
            var guestSession = GuestSession.Example();
            guestSession.GuestSessionState = GuestState.InProject;

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession>() { guestSession });

            // Edge case, session record was deleted in-between the time of being retrieved and being updated
            _guestSessionRepositoryMock
                .Setup(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestSession>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .Throws<DocumentNotFoundException>();

            await Assert.ThrowsAnyAsync<Exception>(async () => await _target.CreateGuestSessionAsync(guestSession));
        }

        [Fact]
        public async Task DeleteGuestSessionsForProjectAsyncKillsAllActiveSessions()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession>
                {
                    new GuestSession { GuestSessionState = GuestState.InLobby },
                    new GuestSession { GuestSessionState = GuestState.InProject },
                    new GuestSession { GuestSessionState = GuestState.Ended }
                });

            await _target.DeleteGuestSessionsForProjectAsync(_defaultGuestSession.ProjectId, false);
            _guestSessionRepositoryMock.Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestSession>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task DeleteGuestSessionsForProjectAsyncKillsInProjectSessions()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession>
                {
                    new GuestSession { GuestSessionState = GuestState.InLobby },
                    new GuestSession { GuestSessionState = GuestState.InProject },
                    new GuestSession { GuestSessionState = GuestState.Ended }
                });

            await _target.DeleteGuestSessionsForProjectAsync(_defaultGuestSession.ProjectId, true);
            _guestSessionRepositoryMock.Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestSession>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteGuestSessionsForProjectAsyncCalculatesProjectLobbyState()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession>
                {
                    new GuestSession { GuestSessionState = GuestState.InLobby },
                    new GuestSession { GuestSessionState = GuestState.InProject },
                    new GuestSession { GuestSessionState = GuestState.Ended }
                });

            await _target.DeleteGuestSessionsForProjectAsync(_defaultGuestSession.ProjectId, true);
            _projectLobbyStateController.Verify(x => x.RecalculateProjectLobbyStateAsync(It.IsAny<Guid>()), Times.Once);
        }

        [Fact]
        public async Task DeleteGuestSessionsForProjectAsyncPublishesGuestSessionsForProjectDeleted()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession>());

            await _target.DeleteGuestSessionsForProjectAsync(_defaultGuestSession.ProjectId, false);

            _eventServiceMock.Verify(x => x.PublishAsync(It.Is<ServiceBusEvent<GuidEvent>>(y => y.Name == EventNames.GuestSessionsForProjectDeleted)));
        }

        [Fact]
        public async Task DeleteGuestSessionsForProjectAsyncPublishesProjectStatusUpdated()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession>());

            await _target.DeleteGuestSessionsForProjectAsync(_defaultGuestSession.ProjectId, false);

            _eventServiceMock.Verify(x => x.PublishAsync(It.Is<ServiceBusEvent<GuidEvent>>(y => y.Name == EventNames.ProjectStatusUpdated)));
        }

        [Fact]
        public async Task EmailHostThrowsNotFoundExceptionForGuestSession()
        {
            _userApiMock
                .Setup(x => x.GetUserAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, User.Example()));

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new NotFoundException("GuestSession could not be found."));

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.EmailHostAsync(_defaultGuestSession.ProjectAccessCode, Guid.NewGuid()));
        }

        [Fact]
        public async Task EmailHostThrowsNotFoundExceptionForProject()
        {
            _userApiMock
                .Setup(x => x.GetUserAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, User.Example()));

            _projectApiMock
                .Setup(x => x.GetProjectByAccessCodeAsync(It.IsAny<string>()))
                .ThrowsAsync(new NotFoundException("Project could not be found"));

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.EmailHostAsync(_defaultGuestSession.ProjectAccessCode, Guid.NewGuid()));
        }

        [Fact]
        public async Task EmailHostThrowsNotFoundExceptionForUser()
        {
            _userApiMock
                .Setup(x => x.GetUserAsync(It.IsAny<Guid>()))
                .ThrowsAsync(new NotFoundException("The sending user could not be found"));

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.EmailHostAsync(_defaultGuestSession.ProjectAccessCode, Guid.NewGuid()));
        }

        [Fact]
        public async Task GetGuestSessionCallsGet()
        {
            var id = Guid.NewGuid();
            await _target.GetGuestSessionAsync(id);
            _guestSessionRepositoryMock.Verify(x => x.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task GetGuestSessionReturnsProjectIfExists()
        {
            var result = await _target.GetGuestSessionAsync(Guid.NewGuid());
            Assert.IsType<GuestSession>(result);
        }

        [Fact]
        public async Task GetGuestSessionThrowsNotFoundOnDocumentNotFound()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .Throws(new NotFoundException("GuestSession could not be found."));

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.GetGuestSessionAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task UpdateGuestSessionThrowsNotFoundOnNotFoundException()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.UpdateItemAsync(It.IsAny<Guid>(), _defaultGuestSession, It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .Throws(new NotFoundException("Message"));

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.UpdateGuestSessionAsync(_defaultGuestSession));
        }

        [Fact]
        public async Task UpdateGuestSessionVerifyCalled()
        {
            await _target.UpdateGuestSessionAsync(_defaultGuestSession);
            _guestSessionRepositoryMock.Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestSession>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task UpdateOfGuestIniviteBussesEvent()
        {
            await _target.UpdateGuestSessionAsync(_defaultGuestSession);
            _eventServiceMock.Verify(x => x.PublishAsync(It.IsAny<ServiceBusEvent<GuestSession>>()));
        }

        [Fact]
        public async Task UpdateGuestSessionStateThrowsValidationExceptionIfProjectWithInvalidGuestAccessCodeIsReturned()
        {
            _validatorMock
                .Setup(v => v.Validate(It.IsAny<object>()))
                .Returns(FailedValidationResult);

            _projectApiMock.Setup(x => x.GetProjectByIdAsync(_defaultGuestSession.ProjectId))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new Project
                {
                    Id = _defaultGuestSession.ProjectId,
                    GuestAccessCode = "INVALID"
                }));

            await Assert.ThrowsAsync<ValidationFailedException>(async () => await _target.UpdateGuestSessionStateAsync(new UpdateGuestSessionStateRequest
            {
                GuestSessionId = _defaultGuestSession.Id,
                GuestSessionState = GuestState.Ended
            }));
        }
    }
}