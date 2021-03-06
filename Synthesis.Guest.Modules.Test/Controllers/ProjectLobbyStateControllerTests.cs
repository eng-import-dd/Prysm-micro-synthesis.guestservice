﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Synthesis.Cache;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.Enumerations;
using Synthesis.GuestService.InternalApi.Enums;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.Utilities.Interfaces;
using Synthesis.Http.Microservice;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.ParticipantService.InternalApi.Models;
using Synthesis.ParticipantService.InternalApi.Services;
using Synthesis.ParticipantService.InternalApi.Testing;
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
        private readonly Mock<ICache> _cacheMock = new Mock<ICache>();
        private readonly Mock<ICacheSelector> _cacheSelectorMock = new Mock<ICacheSelector>();
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
            _cacheSelectorMock.Setup(x => x[It.IsAny<CacheConnection>()])
                .Returns(_cacheMock.Object);

            var repositoryFactoryMock = new Mock<IRepositoryFactory>();
            repositoryFactoryMock
                .Setup(m => m.CreateRepository<GuestSession>())
                .Returns(_guestSessionRepositoryMock.Object);

            repositoryFactoryMock
               .Setup(m => m.CreateRepository<GuestSession>())
               .Returns(_guestSessionRepositoryMock.Object);

            _cacheMock
                .Setup(c => c.TryItemGetAsync(It.IsAny<string>(), typeof(ProjectLobbyState), It.IsAny<Reference<ProjectLobbyState>>()))
                .ReturnsAsync(true);

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
                _cacheSelectorMock.Object,
                validatorLocatorMock.Object,
                _sessionServiceMock.Object,
                _projectApi.Object,
                _eventServiceMock.Object,
                loggerFactoryMock.Object,
                MaxNumberOfGuests);
        }

        [Fact]
        public async Task CreateProjectLobbyState_IfInvalid_ThrowsValidationFailedException()
        {
            _validatorMock
                .Setup(m => m.Validate(It.IsAny<object>()))
                .Returns(FailedValidationResult);

            await Assert.ThrowsAsync<ValidationFailedException>(() => _target.CreateProjectLobbyStateAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task CreateProjectLobbyStateAsync_CreatesNewProjectLobbyState()
        {
            var projectId = Guid.NewGuid();
            await _target.CreateProjectLobbyStateAsync(projectId);
            _cacheMock.Verify(m => m.ItemSetAsync(
                It.IsAny<string>(),
                It.Is<ProjectLobbyState>(s => s.LobbyState == LobbyState.Normal && s.ProjectId == projectId),
                It.IsAny<TimeSpan>(),
                It.IsAny<CacheCommandOptions>()));
        }

        [Fact]
        public async Task RecalculateProjectLobbyStateAsync_IfInvalid_ThrowsValidationFailedException()
        {
            _validatorMock
                .Setup(m => m.Validate(It.IsAny<object>()))
                .Returns(FailedValidationResult);

            await Assert.ThrowsAsync<ValidationFailedException>(() => _target.RecalculateProjectLobbyStateAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task RecalculateProjectLobbyStateAsync_RetrievesParticipant()
        {
            SetupApisForRecalculate();
            await _target.RecalculateProjectLobbyStateAsync(Guid.NewGuid());
            _sessionServiceMock.Verify(m => m.GetParticipantsByGroupNameAsync(It.IsAny<string>(), It.IsAny<bool>(), false));
        }

        [Fact]
        public async Task RecalculateProjectLobbyStateAsync_RetrievesProject()
        {
            SetupApisForRecalculate();
            await _target.RecalculateProjectLobbyStateAsync(Guid.NewGuid());
            _projectApi.Verify(m => m.GetProjectByIdAsync(It.IsAny<Guid>(), null));
        }

        [Fact]
        public async Task RecalculateProjectLobbyStateAsync_RetrievesGuests()
        {
            SetupApisForRecalculate();
            await _target.RecalculateProjectLobbyStateAsync(Guid.NewGuid());
            _guestSessionRepositoryMock.Verify(m => m.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()));
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest, false)]
        [InlineData(HttpStatusCode.OK, true)]
        public async Task RecalculateProjectLobbyStateAsync_IfOneOrMoreApiCallFails_SetsLobbyStateToError(HttpStatusCode projectStatusCode, bool participantRequestFails)
        {
            SetupApisForRecalculate(projectStatusCode, participantRequestFails);

            _cacheMock.Setup(m => m.ItemGetAsync(It.IsAny<List<string>>(), typeof(ProjectLobbyState)))
                .ReturnsAsync((List<ProjectLobbyState>)null);

            _cacheMock
                .Setup(m => m.ItemSetAsync(It.IsAny<string>(), It.IsAny<ProjectLobbyState>(), It.IsAny<TimeSpan>(), It.IsAny<CacheCommandOptions>()))
                .Throws(new ApplicationException());
            var projectId = Guid.NewGuid();

            var result = await _target.RecalculateProjectLobbyStateAsync(projectId);

            Assert.Equal(projectId, result.ProjectId);
            Assert.Equal(LobbyState.Error, result.LobbyState);
        }

        [Fact]
        public async Task RecalculateProjectLobbyStateAsync_UpdatesLobbyState()
        {
            SetupApisForRecalculate();
            await _target.RecalculateProjectLobbyStateAsync(Guid.NewGuid());

            _cacheMock.Verify(m => m.ItemSetAsync(It.IsAny<string>(), It.IsAny<ProjectLobbyState>(), It.IsAny<TimeSpan>(), It.IsAny<CacheCommandOptions>()));
        }

        [Fact]
        public async Task RecalculateProjectLobbyStateAsync_PublishesEvent()
        {
            SetupApisForRecalculate();
            await _target.RecalculateProjectLobbyStateAsync(Guid.NewGuid());

            _eventServiceMock.Verify(m => m.PublishAsync<ProjectLobbyState>(It.IsAny<ServiceBusEvent<ProjectLobbyState>>()), Times.Once);
        }

        [Fact]
        public async Task RecalculateProjectLobbyStateAsync_IfNotFoundAndProjectExists_CreatesAndReturnsLobbyState()
        {
            SetupApisForRecalculate();

            var stateRef = new Reference<ProjectLobbyState> { Value = ProjectLobbyState.Example() };

            _cacheMock
                .SetupSequence(m => m.TryItemGetAsync(It.IsAny<string>(), typeof(ProjectLobbyState), stateRef))
                .ReturnsAsync(false)
                .ReturnsAsync(true);

            _cacheMock
                .Setup(m => m.ItemSetAsync(It.IsAny<string>(), It.IsAny<ProjectLobbyState>(), It.IsAny<TimeSpan>(), It.IsAny<CacheCommandOptions>()))
                .Returns(Task.FromResult<object>(null));

            var result = await _target.RecalculateProjectLobbyStateAsync(Guid.NewGuid());
            Assert.IsAssignableFrom<ProjectLobbyState>(result);
        }

        [Fact]
        public async Task RecalculateProjectLobbyStateAsync_IfProjectDoesNotExist_ThrowsNotFound()
        {
            SetupApisForRecalculate(HttpStatusCode.NotFound);
            var stateRef = new Reference<ProjectLobbyState> { Value = default(ProjectLobbyState) };

            _cacheMock
                .Setup(m => m.TryItemGetAsync(It.IsAny<string>(), typeof(ProjectLobbyState), stateRef))
                .ReturnsAsync(true);

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
        public async Task RecalculateProjectLobbyStateAsync_ReturnsExpectedLobbyState(int fullMemberParticipantCount, int guestSessionCount, LobbyState lobbyState)
        {
            var project = Project.Example();
            var projectId = project.Id;

            var participants = new List<Participant>();
            for (int i = 1; i <= fullMemberParticipantCount; i++)
            {
                var participant = Participant.Example();
                ParticipantExtensions.SetProjectId(participant, project.Id);
                participant.GuestSessionId = null;
                participant.IsGuest = false;
                participants.Add(participant);
            }

            _sessionServiceMock.Setup(m => m.GetParticipantsByGroupNameAsync(It.IsAny<string>(), It.IsAny<bool>(), false))
                .ReturnsAsync(participants);

            var guestSessions = new List<GuestSession>();

            project.GuestAccessCode = Guid.NewGuid().ToString();
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
                .Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(guestSessions);

            _projectApi
                .Setup(m => m.GetProjectByIdAsync(It.IsAny<Guid>(), null))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, project));

            _cacheMock
                .SetupSequence(m => m.ItemGetAsync(It.IsAny<List<string>>(), typeof(ProjectLobbyState)))
                .ReturnsAsync(new List<ProjectLobbyState>() { default(ProjectLobbyState) });

            _cacheMock
                .Setup(m => m.ItemSetAsync(It.IsAny<string>(), It.IsAny<ProjectLobbyState>(), It.IsAny<TimeSpan>(), It.IsAny<CacheCommandOptions>()))
                .Returns(Task.FromResult(ProjectLobbyState.Example()));

            var result = await _target.RecalculateProjectLobbyStateAsync(project.Id);
            Assert.IsAssignableFrom<ProjectLobbyState>(result);
            Assert.Equal(lobbyState, result.LobbyState);
        }

        [Fact]
        public async Task GetProjectLobbyStateAsync_IfInvalid_ThrowsValidationFailedException()
        {
            _validatorMock
                .Setup(m => m.Validate(It.IsAny<object>()))
                .Returns(FailedValidationResult);

            await Assert.ThrowsAsync<ValidationFailedException>(() => _target.GetProjectLobbyStateAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task GetProjectLobbyStateAsync_IfNotFoundAndProjectExists_ReturnsState()
        {
            var project = Project.Example();
            SetupApisForRecalculate(HttpStatusCode.OK, false, project);

            var state = ProjectLobbyState.Example();
            state.ProjectId = project.Id;

            var participants = new List<Participant>();
            var participant = Participant.Example();
            ParticipantExtensions.SetProjectId(participant, project.Id);
            participant.GuestSessionId = null;
            participant.IsGuest = false;
            participants.Add(participant);

            _sessionServiceMock.Setup(m => m.GetParticipantsByGroupNameAsync(It.IsAny<string>(), It.IsAny<bool>(), false))
                .ReturnsAsync(participants);

            _cacheMock
                .Setup(m => m.ItemSetAsync(It.IsAny<string>(), It.IsAny<ProjectLobbyState>(), It.IsAny<TimeSpan>(), It.IsAny<CacheCommandOptions>()))
                .Returns(Task.FromResult(state));

            _cacheMock
                .SetupSequence(m => m.ItemGetAsync(It.IsAny<List<string>>(), typeof(ProjectLobbyState)))
                .ReturnsAsync(default(List<ProjectLobbyState>))
                .ReturnsAsync(new List<ProjectLobbyState>() { state });

            var result = await _target.GetProjectLobbyStateAsync(project.Id);
            Assert.IsAssignableFrom<ProjectLobbyState>(result);
            Assert.Equal(state.ProjectId, result.ProjectId);
            Assert.Equal(state.LobbyState, result.LobbyState);
        }

        [Fact]
        public async Task GetProjectLobbyStateAsync_IfProjectDoesNotExist_ThrowsNotFound()
        {
            _cacheMock
                .Setup(m => m.TryItemGetAsync(It.IsAny<string>(), typeof(ProjectLobbyState), It.IsAny<Reference<ProjectLobbyState>>()))
                .ReturnsAsync(false);

            _projectApi
                .Setup(m => m.GetProjectByIdAsync(It.IsAny<Guid>(), null))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.NotFound, default(Project)));

            await Assert.ThrowsAsync<NotFoundException>(() => _target.GetProjectLobbyStateAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task GetProjectLobbyStateAsync_RetrievesLobbyState()
        {
            var state = ProjectLobbyState.Example();
            _cacheMock
                .Setup(m => m.ItemGetAsync(It.IsAny<IEnumerable<string>>(), typeof(ProjectLobbyState)))
                .ReturnsAsync(new List<ProjectLobbyState>() { state });

            var result = await _target.GetProjectLobbyStateAsync(Guid.NewGuid());
            Assert.IsAssignableFrom<ProjectLobbyState>(result);
            Assert.Equal(state, result);
        }

        [Fact]
        public async Task DeleteProjectLobbyStateAsync_DeletesProjectLobbyState()
        {
            _cacheMock.Setup(m => m.KeyDeleteAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CacheCommandOptions>()))
                .ReturnsAsync(1);

            await _target.DeleteProjectLobbyStateAsync(Guid.NewGuid());
            _cacheMock.Verify(m => m.KeyDeleteAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CacheCommandOptions>()));
        }

        private void SetupApisForRecalculate(HttpStatusCode projectStatusCode = HttpStatusCode.OK, bool participantRequestThrows = false, Project project = null)
        {
            if (participantRequestThrows)
            {
                var taskSource = new TaskCompletionSource<IEnumerable<Participant>>();
                taskSource.SetException(new Exception("participants failed"));
                _sessionServiceMock.Setup(m => m.GetParticipantsByGroupNameAsync(It.IsAny<string>(), It.IsAny<bool>(), false))
                    .Returns(taskSource.Task);
            }
            else
            {
                _sessionServiceMock.Setup(m => m.GetParticipantsByGroupNameAsync(It.IsAny<string>(), It.IsAny<bool>(), false))
                    .ReturnsAsync(default(IEnumerable<Participant>));
            }

            var projectPayload = projectStatusCode == HttpStatusCode.OK ? project ?? Project.Example() : default(Project);
            _projectApi
                .Setup(m => m.GetProjectByIdAsync(It.IsAny<Guid>(), null))
                .ReturnsAsync(MicroserviceResponse.Create(projectStatusCode, projectPayload));
        }

        private GuestSession CloneGuestSession(GuestSession guestSession)
        {
            return new GuestSession
            {
                Id = guestSession.Id,
                AccessGrantedBy = guestSession.AccessGrantedBy,
                AccessGrantedDateTime = guestSession.AccessGrantedDateTime,
                AccessRevokedBy = guestSession.AccessRevokedBy,
                AccessRevokedDateTime = guestSession.AccessRevokedDateTime,
                CreatedDateTime = guestSession.CreatedDateTime,
                EmailedHostDateTime = guestSession.EmailedHostDateTime,
                GuestSessionState = guestSession.GuestSessionState,
                ProjectId = guestSession.ProjectId,
                ProjectAccessCode = guestSession.ProjectAccessCode,
                UserId = guestSession.UserId
            };
        }
    }
}