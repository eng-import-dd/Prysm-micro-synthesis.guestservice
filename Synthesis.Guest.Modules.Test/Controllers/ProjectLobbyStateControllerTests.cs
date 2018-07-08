using System;
using System.Collections.Async.Internals;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Moq.Language.Flow;
using StackExchange.Redis;
using Synthesis.Cache;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.InternalApi.Enums;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.Http.Microservice;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.ParticipantService.InternalApi.Models;
using Synthesis.ParticipantService.InternalApi.Services;
using Synthesis.ProjectService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Models;
using Synthesis.Threading.Tasks;
using Xunit;
using LobbyState = Synthesis.GuestService.InternalApi.Enums.LobbyState;

namespace Synthesis.GuestService.Modules.Test.Controllers
{
    public class ProjectLobbyStateControllerTests
    {
        private readonly IProjectLobbyStateController _target;
        private readonly Mock<IRepository<ProjectLobbyState>> _projectLobbyStateRepositoryMock = new Mock<IRepository<ProjectLobbyState>>();
        private readonly Mock<ICache> _cacheMock = new Mock<ICache>();
        private readonly Mock<IRepository<GuestSession>> _guestSessionRepositoryMock = new Mock<IRepository<GuestSession>>();
        private readonly Mock<IValidator> _validatorMock = new Mock<IValidator>();
        private readonly Mock<ISessionService> _sessionServiceMock = new Mock<ISessionService>();
        private readonly Mock<IEventService> _eventServiceMock = new Mock<IEventService>();
        private readonly Mock<IProjectApi> _projectApi = new Mock<IProjectApi>();
        private const int MaxNumberOfGuests = 10;
        private static ValidationResult SuccessfulValidationResult => new ValidationResult();
        private static ValidationResult FailedValidationResult => new ValidationResult(
            new List<ValidationFailure>
            {
                new ValidationFailure(string.Empty, string.Empty)
            }
        );

        public ProjectLobbyStateControllerTests()
        {
            var repositoryFactoryMock = new Mock<IRepositoryFactory>();
            repositoryFactoryMock
                .Setup(m => m.CreateRepository<GuestSession>())
                .Returns(_guestSessionRepositoryMock.Object);

            repositoryFactoryMock
                .Setup(m => m.CreateRepository<GuestSession>())
                .Returns(_guestSessionRepositoryMock.Object);

            //_cacheMock
            //    .Setup(c => c.TryItemGetAsync(It.IsAny<string>(), It.IsAny<Reference<ProjectLobbyState>>()))
            //    .ReturnsAsync(true);

            _validatorMock
                .Setup(v => v.Validate(It.IsAny<object>()))
                .Returns(SuccessfulValidationResult);

            var validatorLocatorMock = new Mock<IValidatorLocator>();
            validatorLocatorMock
                .Setup(m => m.GetValidator(It.IsAny<Type>()))
                .Returns(_validatorMock.Object);

            var loggerFactoryMock = new Mock<ILoggerFactory>();
            loggerFactoryMock.Setup(m => m.Get(It.IsAny<LogTopic>()))
                .Returns(new Mock<ILogger>().Object);

            _target = new ProjectLobbyStateController(repositoryFactoryMock.Object,
                _cacheMock.Object,
                validatorLocatorMock.Object,
                _sessionServiceMock.Object,
                _projectApi.Object,
                _eventServiceMock.Object,
                loggerFactoryMock.Object,
                MaxNumberOfGuests);
        }

        [Fact]
        public async Task CreateProjectLobbyStateThrowsValidationFailedExceptionIfInvalid()
        {
            _validatorMock
                .Setup(m => m.Validate(It.IsAny<object>()))
                .Returns(FailedValidationResult);

            await Assert.ThrowsAsync<ValidationFailedException>(() => _target.CreateProjectLobbyStateAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task CreateProjectLobbyStateAsyncCreatesNewProjectLobbyState()
        {
            var projectId = Guid.NewGuid();
            await _target.CreateProjectLobbyStateAsync(projectId);
            _cacheMock.Verify(m => m.ItemSetAsync(
                It.IsAny<string>(),
                It.Is<ProjectLobbyState>(s => s.LobbyState == LobbyState.Normal && s.ProjectId == projectId),
                It.IsAny<TimeSpan>()));
        }

        [Fact]
        public async Task RecalculateProjectLobbyStateAsyncThrowsValidationFailedExceptionIfInvalid()
        {
            _validatorMock
                .Setup(m => m.Validate(It.IsAny<object>()))
                .Returns(FailedValidationResult);

            await Assert.ThrowsAsync<ValidationFailedException>(() => _target.RecalculateProjectLobbyStateAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task RecalculateProjectLobbyStateAsyncRetrievesParticipant()
        {
            SetupApisForRecalculate();
            await _target.RecalculateProjectLobbyStateAsync(Guid.NewGuid());
            _sessionServiceMock.Verify(m => m.GetParticipantsByGroupNameAsync(It.IsAny<string>(), It.IsAny<bool>()));
        }

        [Fact]
        public async Task RecalculateProjectLobbyStateAsyncRetrievesProject()
        {
            SetupApisForRecalculate();
            await _target.RecalculateProjectLobbyStateAsync(Guid.NewGuid());
            _projectApi.Verify(m => m.GetProjectByIdAsync(It.IsAny<Guid>()));
        }

        [Fact]
        public async Task RecalculateProjectLobbyStateAsyncRetrievesGuests()
        {
            SetupApisForRecalculate();
            await _target.RecalculateProjectLobbyStateAsync(Guid.NewGuid());
            _guestSessionRepositoryMock.Verify(m => m.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>()));
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest, false)]
        [InlineData(HttpStatusCode.OK, true)]
        public async Task RecalculateProjectLobbyStateAsyncSetsLobbyStateToErrorIfOneOrMoreApiCallFails(HttpStatusCode projectStatusCode, bool participantRequestFails)
        {
            SetupApisForRecalculate(projectStatusCode, participantRequestFails);


            _cacheMock
                .Setup(m => m.ItemSetAsync(It.IsAny<string>(), It.IsAny<ProjectLobbyState>(), It.IsAny<TimeSpan>()))
                .Throws(new ApplicationException());
            var projectId = Guid.NewGuid();
            var expectedResult = new ProjectLobbyState(){ProjectId = projectId, LobbyState = LobbyState.Error};

            var result = await _target.RecalculateProjectLobbyStateAsync(projectId);


            Assert.Equal(projectId, result.ProjectId);
            Assert.Equal(LobbyState.Error, result.LobbyState);
            //_projectLobbyStateRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.Is<ProjectLobbyState>(state => state.LobbyState == LobbyState.Error)));
        }

        [Fact]
        public async Task RecalculateProjectLobbyStateAsyncUpdatesLobbyState()
        {
            SetupApisForRecalculate();
            await _target.RecalculateProjectLobbyStateAsync(Guid.NewGuid());
            _projectLobbyStateRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<ProjectLobbyState>()));
        }

        [Fact]
        public async Task RecalculateProjectLobbyStateAsyncCreatesAndReturnsLobbyStateIfNotFoundAndProjectExists()
        {
            SetupApisForRecalculate();
            _projectLobbyStateRepositoryMock
                .Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult(default(ProjectLobbyState)));

            _projectLobbyStateRepositoryMock
                .Setup(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<ProjectLobbyState>()))
                .Throws<DocumentNotFoundException>();

            _projectLobbyStateRepositoryMock
                .Setup(m => m.CreateItemAsync(It.IsAny<ProjectLobbyState>()))
                .Returns(Task.FromResult(ProjectLobbyState.Example()));


            var result = await _target.RecalculateProjectLobbyStateAsync(Guid.NewGuid());
            Assert.IsAssignableFrom<ProjectLobbyState>(result);
        }

        [Fact]
        public async Task RecalculateProjectLobbyStateAsyncThrowsNotFoundIfProjectDoesNotExist()
        {
            SetupApisForRecalculate(HttpStatusCode.NotFound, false);
            _projectLobbyStateRepositoryMock
                .Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult(default(ProjectLobbyState)));

            await Assert.ThrowsAsync<NotFoundException>(() => _target.RecalculateProjectLobbyStateAsync(Guid.NewGuid()));
        }

        [Theory]
        [InlineData(5, 9, LobbyState.Normal)]
        [InlineData(1, 0, LobbyState.Normal)]
        [InlineData(10, 0, LobbyState.Normal)]
        [InlineData(11, 0, LobbyState.Normal)]
        [InlineData(0, 1, LobbyState.HostNotPresent)]
        [InlineData(0, 10, LobbyState.HostNotPresent)]
        [InlineData(0, 11, LobbyState.HostNotPresent)]
        [InlineData(0, 0, LobbyState.HostNotPresent)]
        [InlineData(1, 10, LobbyState.GuestLimitReached)]
        [InlineData(5, 10, LobbyState.GuestLimitReached)]
        [InlineData(1, 11, LobbyState.GuestLimitReached)]
        [InlineData(5, 11, LobbyState.GuestLimitReached)]
        [InlineData(20, 10, LobbyState.GuestLimitReached)]
        [InlineData(20, 11, LobbyState.GuestLimitReached)]
        public async Task RecalculateProjectLobbyStateAsyncReturnsExpectedLobbyState(int fullMemberParticipantCount, int guestSessionCount, LobbyState lobbyState)
        {

            var project = Project.Example();
            var projectId = project.Id;

            var participants = new List<Participant>();
            for (int i = 1; i <= fullMemberParticipantCount; i++)
            {
                var participant = Participant.Example();
                participant.ProjectId = projectId;
                participant.GuestSessionId = (Guid?)null;
                participant.IsGuest = false;
                participants.Add(participant);
            }

            _sessionServiceMock.Setup(m => m.GetParticipantsByGroupNameAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(participants);

            var guestSessions = new List<GuestSession>();

            project.GuestAccessCode = "0123456789";
            for (int i = 1; i <= guestSessionCount; i++)
            {
                var guestSession = GuestSession.Example();
                guestSession.ProjectId = projectId;
                guestSession.ProjectAccessCode = project.GuestAccessCode;
                guestSession.GuestSessionState = GuestState.InProject;
                guestSession.CreatedDateTime = DateTime.UtcNow;
                guestSession.UserId = Guid.NewGuid();
                guestSessions.Add(guestSession);

                // Should never have more than one InProject sessions for same user and project,
                // but need to test LINQ query with group by, where, and order by clauses,
                // for correct calculation of current guest quantity.
                var guestSession2 = CloneGuestSession(guestSession);
                guestSession2.CreatedDateTime = DateTime.UtcNow.AddHours(-1.0);
                guestSessions.Add(guestSession2);

                var guestSession3 = CloneGuestSession(guestSession);
                guestSession3.CreatedDateTime = DateTime.UtcNow.AddHours(-2.0);
                guestSessions.Add(guestSession3);

            }

            _guestSessionRepositoryMock
                .Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>()))
                .ReturnsAsync(guestSessions);

            _projectApi
                .Setup(m => m.GetProjectByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, project));

            _projectLobbyStateRepositoryMock
                .Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult(default(ProjectLobbyState)));

            _projectLobbyStateRepositoryMock
                .Setup(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<ProjectLobbyState>()))
                .Throws<DocumentNotFoundException>();

            _projectLobbyStateRepositoryMock
                .Setup(m => m.CreateItemAsync(It.IsAny<ProjectLobbyState>()))
                .ReturnsAsync((ProjectLobbyState state) => state);


            var result = await _target.RecalculateProjectLobbyStateAsync(project.Id);
            Assert.IsAssignableFrom<ProjectLobbyState>(result);
            Assert.Equal(lobbyState, result.LobbyState);
        }

        [Fact]
        public async Task GetProjectLobbyStateAsyncThrowsValidationFailedExceptionIfInvalid()
        {
            _validatorMock
                .Setup(m => m.Validate(It.IsAny<object>()))
                .Returns(FailedValidationResult);

            await Assert.ThrowsAsync<ValidationFailedException>(() => _target.GetProjectLobbyStateAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task GetProjectLobbyStateAsyncReturnsStateIfNotFoundAndProjectExists()
        {

            //_projectLobbyStateRepositoryMock
            //    .Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
            //    .Returns(Task.FromResult(ProjectLobbyState.Example()));

            var state = new Mock<ProjectLobbyState>().Object;
            _cacheMock
                .Setup(m => m.TryItemGet<ProjectLobbyState>(It.IsAny<string>(), out state))
                .Returns(false);

            SetupApisForRecalculate();

            var result = await _target.GetProjectLobbyStateAsync(Guid.NewGuid());
            Assert.IsAssignableFrom<ProjectLobbyState>(result);
        }

        [Fact]
        public async Task GetProjectLobbyStateAsyncThrowsNotFoundIfProjectDoesNotExist()
        {
            _cacheMock
                .Setup(m => m.TryItemGetAsync(It.IsAny<string>(), It.IsAny<Reference<ProjectLobbyState>>()))
                .ReturnsAsync(false);

            _projectApi
                .Setup(m => m.GetProjectByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.NotFound, default(Project)));

            await Assert.ThrowsAsync<NotFoundException>(() => _target.GetProjectLobbyStateAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task GetProjectLobbyStateAsyncRetrievesLobbyState()
        {
            _cacheMock
                .Setup(m => m.TryItemGetAsync(It.IsAny<string>(), It.IsAny<Reference<ProjectLobbyState>>()))
                .ReturnsAsync(true);

            var result = await _target.GetProjectLobbyStateAsync(Guid.NewGuid());
            Assert.IsAssignableFrom<ProjectLobbyState>(result);
        }

        [Fact]
        public async Task DeleteProjectLobbyStateAsyncDeletesProjectLobbyState()
        {
            await _target.DeleteProjectLobbyStateAsync(Guid.NewGuid());
            _cacheMock.Verify(m => m.KeyDeleteAsync(It.IsAny<string>()));
        }

        private void SetupApisForRecalculate(HttpStatusCode projectStatusCode = HttpStatusCode.OK, bool participantRequestThrows = false)
        {
            if (participantRequestThrows)
            {
                var taskSource = new TaskCompletionSource<IEnumerable<Participant>>();
                taskSource.SetException(new Exception("participants failed"));
                _sessionServiceMock.Setup(m => m.GetParticipantsByGroupNameAsync(It.IsAny<string>(), It.IsAny<bool>()))
                    .Returns(taskSource.Task);
            }
            else
            {
                _sessionServiceMock.Setup(m => m.GetParticipantsByGroupNameAsync(It.IsAny<string>(), It.IsAny<bool>()))
                    .ReturnsAsync(default(IEnumerable<Participant>));
            }

            var project = projectStatusCode == HttpStatusCode.OK ? Project.Example() : default(Project);
            _projectApi
                .Setup(m => m.GetProjectByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(projectStatusCode, project));
        }

        private GuestSession CloneGuestSession(GuestSession guestSession)
        {
            return new GuestSession()
            {
                Id = guestSession.Id,
                AccessGrantedBy = guestSession.AccessGrantedBy,
                AccessGrantedDateTime = guestSession.AccessGrantedDateTime,
                AccessRevokedBy = guestSession.AccessRevokedBy,
                AccessRevokedDateTime = guestSession.AccessRevokedDateTime,
                CreatedDateTime = guestSession.CreatedDateTime,
                Email = guestSession.Email,
                EmailedHostDateTime = guestSession.EmailedHostDateTime,
                FirstName = guestSession.FirstName,
                GuestSessionState = guestSession.GuestSessionState,
                LastName = guestSession.LastName,
                LastAccessDate = guestSession.LastAccessDate,
                ProjectId = guestSession.ProjectId,
                ProjectAccessCode = guestSession.ProjectAccessCode,
                UserId = guestSession.UserId
            };
        }
    }
}
