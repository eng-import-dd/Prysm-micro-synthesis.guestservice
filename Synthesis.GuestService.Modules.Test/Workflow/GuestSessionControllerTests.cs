using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Dao.Models;
using Synthesis.GuestService.Workflow.ApiWrappers;
using Synthesis.GuestService.Workflow.Controllers;
using Synthesis.GuestService.Workflow.Utilities;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Workflow
{
    public class GuestSessionControllerTests
    {
        public GuestSessionControllerTests()
        {
            _defaultGuestSession.Id = Guid.NewGuid();
            _defaultGuestSession.UserId = Guid.NewGuid();
            _defaultGuestSession.ProjectId = Guid.NewGuid();
            _defaultGuestSession.ProjectAccessCode = "123123123123";

            var repositoryFactoryMock = new Mock<IRepositoryFactory>();
            _guestSessionRepositoryMock = new Mock<IRepository<GuestSession>>();

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(_defaultGuestSession);

            _guestSessionRepositoryMock
                .Setup(x => x.CreateItemAsync(It.IsAny<GuestSession>()))
                .ReturnsAsync((GuestSession participant) => participant);

            _guestSessionRepositoryMock
                .Setup(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestSession>()))
                .ReturnsAsync((Guid id, GuestSession participant) => participant);

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
                                                 loggerFactoryMock.Object, _emailUtility.Object, _passwordUtility.Object, _projectApiMock.Object,
                                                 _participantApiMock.Object, _userApiMock.Object, _settingsApiMock.Object);
        }

        private readonly GuestSessionController _target;
        private readonly Mock<IRepository<GuestSession>> _guestSessionRepositoryMock;
        private readonly Mock<IEventService> _eventServiceMock = new Mock<IEventService>();
        private readonly Mock<IEmailUtility> _emailUtility = new Mock<IEmailUtility>();
        private readonly Mock<IPasswordUtility> _passwordUtility = new Mock<IPasswordUtility>();
        private readonly Mock<IProjectApiWrapper> _projectApiMock = new Mock<IProjectApiWrapper>();
        private readonly Mock<ISettingsApiWrapper> _settingsApiMock = new Mock<ISettingsApiWrapper>();
        private readonly Mock<IPrincipalApiWrapper> _userApiMock = new Mock<IPrincipalApiWrapper>();
        private readonly Mock<IParticipantApiWrapper> _participantApiMock = new Mock<IParticipantApiWrapper>();
        private readonly GuestSession _defaultGuestSession = new GuestSession();
        private readonly Mock<IValidator> _validatorMock = new Mock<IValidator>();
        private readonly Mock<IValidatorLocator> _validatorLocator = new Mock<IValidatorLocator>();

        [Fact]
        public async Task CreateGuestSessionCallsCreate()
        {
            await _target.CreateGuestSessionAsync(_defaultGuestSession);
            _guestSessionRepositoryMock.Verify(x => x.CreateItemAsync(It.IsAny<GuestSession>()));
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
        public async Task DeleteGuestSessionsForProjectPublishesGuestSessionsForProjectDeleted()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>()))
                .ReturnsAsync(new List<GuestSession>());

            await _target.DeleteGuestSessionsForProject(_defaultGuestSession.ProjectId, false);

            _eventServiceMock.Verify(x => x.PublishAsync(It.Is<ServiceBusEvent<GuidEvent>>(y => y.Name == EventNames.GuestSessionsForProjectDeleted)));
        }

        [Fact]
        public async Task DeleteGuestSessionsForProjectKillsAllActiveSessions()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>()))
                .ReturnsAsync(new List<GuestSession>()
                {
                    new GuestSession() {GuestSessionState = GuestState.InLobby},
                    new GuestSession() {GuestSessionState = GuestState.InProject},
                    new GuestSession() {GuestSessionState = GuestState.Ended}
                });

            await _target.DeleteGuestSessionsForProject(_defaultGuestSession.ProjectId, false);
            _guestSessionRepositoryMock.Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestSession>()), Times.Exactly(2));
        }

        [Fact]
        public async Task DeleteGuestSessionsForProjectKillsInProjectSessions()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>()))
                .ReturnsAsync(new List<GuestSession>()
                {
                    new GuestSession() {GuestSessionState = GuestState.InLobby},
                    new GuestSession() {GuestSessionState = GuestState.InProject},
                    new GuestSession() {GuestSessionState = GuestState.Ended}
                });

            await _target.DeleteGuestSessionsForProject(_defaultGuestSession.ProjectId, true);
            _guestSessionRepositoryMock.Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestSession>()), Times.Once);
        }

        [Fact]
        public async Task CreateGuestSessionVerifyCalled()
        {
            await _target.CreateGuestSessionAsync(_defaultGuestSession);
            _guestSessionRepositoryMock.Verify(x => x.CreateItemAsync(It.IsAny<GuestSession>()));
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
        public async Task GetGuestSessionCallsGet()
        {
            var id = Guid.NewGuid();
            await _target.GetGuestSessionAsync(id);
            _guestSessionRepositoryMock.Verify(x => x.GetItemAsync(It.IsAny<Guid>()));
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
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>()))
                .Throws(new NotFoundException("GuestSession could not be found."));

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.GetGuestSessionAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task UpdateGuestSessionThrowsNotFoundOnNotFoundException()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.UpdateItemAsync(It.IsAny<Guid>(), _defaultGuestSession))
                .Throws(new NotFoundException("Message"));

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.UpdateGuestSessionAsync(_defaultGuestSession));
        }

        [Fact]
        public async Task UpdateGuestSessionVerifyCalled()
        {
            await _target.UpdateGuestSessionAsync(_defaultGuestSession);
            _guestSessionRepositoryMock.Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestSession>()));
        }

        [Fact]
        public async Task UpdateOfGuestIniviteBussesEvent()
        {
            await _target.UpdateGuestSessionAsync(_defaultGuestSession);
            _eventServiceMock.Verify(x => x.PublishAsync(It.IsAny<ServiceBusEvent<GuestSession>>()));
        }
    }
}