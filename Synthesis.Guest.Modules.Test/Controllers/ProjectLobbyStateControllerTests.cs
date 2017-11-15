﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Synthesis.DocumentStorage;
using Synthesis.GuestService.ApiWrappers.Interfaces;
using Synthesis.GuestService.ApiWrappers.Responses;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.Enums;
using Synthesis.GuestService.Models;
using Synthesis.Http.Microservice;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Controllers
{
    public class ProjectLobbyStateControllerTests
    {
        private readonly IProjectLobbyStateController _target;
        private readonly Mock<IRepository<ProjectLobbyState>> _projectLobbyStateRepositoryMock = new Mock<IRepository<ProjectLobbyState>>();
        private readonly Mock<IRepository<GuestSession>> _guestSessionRepositoryMock = new Mock<IRepository<GuestSession>>();
        private readonly Mock<IValidator> _validatorMock = new Mock<IValidator>();
        private readonly Mock<IParticipantApiWrapper> _participantApiMock = new Mock<IParticipantApiWrapper>();
        private readonly Mock<IProjectApiWrapper> _projectApi = new Mock<IProjectApiWrapper>();
        private const int MaxNumberOfGuests = 0;
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
                .Setup(m => m.CreateRepository<ProjectLobbyState>())
                .Returns(_projectLobbyStateRepositoryMock.Object);

            repositoryFactoryMock
                .Setup(m => m.CreateRepository<GuestSession>())
                .Returns(_guestSessionRepositoryMock.Object);

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
                validatorLocatorMock.Object,
                _participantApiMock.Object,
                _projectApi.Object,
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
            _projectLobbyStateRepositoryMock.Verify(m => m.CreateItemAsync(
                It.Is<ProjectLobbyState>(s => s.LobbyState == LobbyState.Undefined && s.ProjectId == projectId)));
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
            _participantApiMock.Verify(m => m.GetParticipantsByProjectIdAsync(It.IsAny<Guid>()));
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
        [InlineData(HttpStatusCode.BadRequest, HttpStatusCode.OK)]
        [InlineData(HttpStatusCode.OK, HttpStatusCode.BadRequest)]
        public async Task RecalculateProjectLobbyStateAsyncSetsLobbyStateToErrorIfOneOrMoreApiCallFails(HttpStatusCode participantsStatusCode, HttpStatusCode projectStatusCode)
        {
            SetupApisForRecalculate(participantsStatusCode, projectStatusCode);
            await _target.RecalculateProjectLobbyStateAsync(Guid.NewGuid());
            _projectLobbyStateRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.Is<ProjectLobbyState>(state => state.LobbyState == LobbyState.Error)));
        }

        [Fact]
        public async Task RecalculateProjectLobbyStateAsyncUpdatesLobbyState()
        {
            SetupApisForRecalculate();
            await _target.RecalculateProjectLobbyStateAsync(Guid.NewGuid());
            _projectLobbyStateRepositoryMock.Verify(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<ProjectLobbyState>()));
        }

        [Fact]
        public async Task RecalculateProjectLobbyStateAsyncThrowsNotFoundExceptionIfProjectLobbyStateDoesNotExist()
        {
            SetupApisForRecalculate();
            _projectLobbyStateRepositoryMock
                .Setup(m => m.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<ProjectLobbyState>()))
                .Throws<DocumentNotFoundException>();

            await Assert.ThrowsAsync<NotFoundException>(() => _target.RecalculateProjectLobbyStateAsync(Guid.NewGuid()));
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
        public async Task GetProjectLobbyStateAsyncThrowsNotFoundExceptionIfProjectLobbyStateDoesNotExist()
        {
            _projectLobbyStateRepositoryMock
                .Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult(default(ProjectLobbyState)));

            await Assert.ThrowsAsync<NotFoundException>(() => _target.GetProjectLobbyStateAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task GetProjectLobbyStateAsyncRetrievesLobbyState()
        {
            _projectLobbyStateRepositoryMock
                .Setup(m => m.GetItemAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult(new ProjectLobbyState()));

            var result = await _target.GetProjectLobbyStateAsync(Guid.NewGuid());
            Assert.IsAssignableFrom<ProjectLobbyState>(result);
        }

        [Fact]
        public async Task DeleteProjectLobbyStateAsyncDeletesProjectLobbyState()
        {
            await _target.DeleteProjectLobbyStateAsync(Guid.NewGuid());
            _projectLobbyStateRepositoryMock.Verify(m => m.DeleteItemAsync(It.IsAny<Guid>()));
        }

        private void SetupApisForRecalculate(HttpStatusCode participantsStatusCode = HttpStatusCode.OK,
            HttpStatusCode projectStatusCode = HttpStatusCode.OK)
        {
            _participantApiMock
                .Setup(m => m.GetParticipantsByProjectIdAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult(MicroserviceResponse.Create(participantsStatusCode, default(IEnumerable<ParticipantResponse>))));

            _projectApi
                .Setup(m => m.GetProjectByIdAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult(MicroserviceResponse.Create(projectStatusCode, default(ProjectResponse))));
        }
    }
}