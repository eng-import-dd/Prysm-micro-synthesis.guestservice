﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
using Synthesis.Guest.ProjectContext.Models;
using Synthesis.Guest.ProjectContext.Services;
using Synthesis.GuestService.InternalApi.Constants;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.InternalApi.Enums;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.InternalApi.Requests;
using Synthesis.GuestService.Modules.Test.Extensions;
using Synthesis.GuestService.Utilities.Interfaces;
using Synthesis.GuestService.Validators;
using Synthesis.Http.Microservice;
using Synthesis.Http.Microservice.Models;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.InternalApi.Api;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.ProjectService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Models;
using Synthesis.Serialization;
using Synthesis.SettingService.InternalApi.Api;
using Synthesis.SettingService.InternalApi.Models;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Controllers
{
    public class GuestSessionControllerTests
    {
        private readonly GuestSessionController _target;
        private readonly Mock<IRepository<GuestSession>> _guestSessionRepositoryMock;
        private readonly Mock<IRepository<GuestInvite>> _guestInviteRepositoryMock;
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
        private readonly Mock<IProjectLobbyStateController> _projectLobbyStateControllerMock = new Mock<IProjectLobbyStateController>();
        private readonly Mock<IObjectSerializer> _synthesisObjectSerializer = new Mock<IObjectSerializer>();
        private readonly Mock<IProjectGuestContextService> _projectGuestContextServiceMock = new Mock<IProjectGuestContextService>();
        private readonly Mock<IValidator> _validatorFailsMock = new Mock<IValidator>();

        private readonly User _defaultUser = User.Example();
        private readonly Project _defaultProject;
        private readonly Guid _defaultPrincipalId;
        private readonly Guid _defaultTenantId;
        private readonly Guid _defaultProjectAccessCode = Guid.NewGuid();
        private readonly GuestVerificationRequest _defaultGuestVerificationRequest;
        private readonly UpdateGuestSessionStateRequest _updateGuestSessionStateRequest = UpdateGuestSessionStateRequest.Example;

        private static ValidationResult FailedValidationResult => new ValidationResult(
            new List<ValidationFailure>
            {
                new ValidationFailure(string.Empty, string.Empty)
            }
        );

        public GuestSessionControllerTests()
        {
            _defaultUser.IsEmailVerified = true;

            _defaultProject = Project.Example();
            _defaultPrincipalId = Guid.NewGuid();
            _defaultTenantId = Guid.NewGuid();
            _defaultGuestSession.Id = Guid.NewGuid();
            _defaultGuestSession.UserId = Guid.NewGuid();
            _defaultGuestSession.ProjectId = _defaultProject.Id;
            _defaultGuestSession.ProjectAccessCode = _defaultProject.GuestAccessCode;
            _defaultGuestSession.GuestSessionState = GuestState.InLobby;

            _defaultProject.GuestAccessCode = _defaultProjectAccessCode.ToString();

            _defaultGuestVerificationRequest = new GuestVerificationRequest
            {
                ProjectAccessCode = _defaultProjectAccessCode.ToString(),
                Username = "userName",
                ProjectId = _defaultProject.Id
            };

            var repositoryFactoryMock = new Mock<IRepositoryFactory>();
            _guestSessionRepositoryMock = new Mock<IRepository<GuestSession>>();
            _guestInviteRepositoryMock = new Mock<IRepository<GuestInvite>>();

            _projectGuestContextServiceMock.Setup(x => x.GetProjectGuestContextAsync(It.IsAny<string>())).ReturnsAsync(new ProjectGuestContext
            {
                GuestSessionId = _defaultGuestSession.Id,
                GuestState = Guest.ProjectContext.Enums.GuestState.InLobby,
                ProjectId = _defaultGuestSession.ProjectId,
                TenantId = Guid.NewGuid()
            });

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_defaultGuestSession);

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession> {_defaultGuestSession});

            _guestSessionRepositoryMock
                .Setup(x => x.CreateItemAsync(It.IsAny<GuestSession>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((GuestSession participant, CancellationToken c) => participant);

            _guestSessionRepositoryMock
                .Setup(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestSession>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, GuestSession participant, UpdateOptions o, CancellationToken c) => participant);

            _guestInviteRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_defaultGuestInvite);

            _guestInviteRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestInvite, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestInvite> { _defaultGuestInvite });

            _guestInviteRepositoryMock
                .Setup(x => x.CreateItemAsync(It.IsAny<GuestInvite>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((GuestInvite session, CancellationToken c) => session);

            _guestInviteRepositoryMock
                .Setup(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestInvite>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, GuestInvite session, UpdateOptions o, CancellationToken c) => session);

            _serviceToServiceProjectApiMock
                .Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<KeyValuePair<string, string>>>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, _defaultProject));

            _projectApiMock
                .Setup(x => x.GetProjectByAccessCodeAsync(It.IsAny<string>(), null))
                .ThrowsAsync(new NotFoundException("Project could not be found"));

            _userApiMock.Setup(x => x.GetUserByUserNameOrEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, _defaultUser));

            _settingsApiMock.Setup(x => x.GetUserSettingsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new UserSettings { IsGuestModeEnabled = true }));

            _validatorFailsMock.Setup(m => m.Validate(It.IsAny<object>()))
                .Returns(new ValidationResult { Errors = { new ValidationFailure(string.Empty, string.Empty) } });

            repositoryFactoryMock
#pragma warning disable 612
                .Setup(x => x.CreateRepository<GuestSession>())
#pragma warning restore 612
                .Returns(_guestSessionRepositoryMock.Object);

            repositoryFactoryMock
#pragma warning disable 612
                .Setup(x => x.CreateRepository<GuestInvite>())
#pragma warning restore 612
                .Returns(_guestInviteRepositoryMock.Object);

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

            var sessionIdStringHeader = new KeyValuePair<string, IEnumerable<string>>("SessionIdString", new List<string> { Guid.NewGuid().ToString() });
            var sessionIdHeader = new KeyValuePair<string, IEnumerable<string>>("SessionId", new List<string> { Guid.NewGuid().ToString() });
            var kvpList = new List<KeyValuePair<string, IEnumerable<string>>>(2) { sessionIdStringHeader, sessionIdHeader };
            var headersWithSession = new RequestHeaders(kvpList);

            _target = new GuestSessionController(repositoryFactoryMock.Object, _validatorLocator.Object, _eventServiceMock.Object,
                                                 loggerFactoryMock.Object, _emailUtility.Object, _projectApiMock.Object, _serviceToServiceProjectApiMock.Object,
                                                 _userApiMock.Object, _projectLobbyStateControllerMock.Object, _settingsApiMock.Object, _synthesisObjectSerializer.Object,
                                                 _projectGuestContextServiceMock.Object,headersWithSession);
        }

        #region UpdateGuestSessionStateAsync
        [Fact]
        public async Task UpdateGuestSessionStateAsync_IfCannotGetSession_ThrowsInvalidOperationException()
        {
            _guestSessionRepositoryMock.Setup(x => x.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("some exception"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => _target.UpdateGuestSessionStateAsync(_updateGuestSessionStateRequest, Guid.NewGuid()));
        }

        [Fact]
        public async Task UpdateGuestSessionStateAsync_WhenUpdatingAnEndedSession_ReturnsSessionEnded()
        {
            _defaultGuestSession.GuestSessionState = GuestState.Ended;

            var reponse = await _target.UpdateGuestSessionStateAsync(_updateGuestSessionStateRequest, Guid.NewGuid());

            Assert.Equal(UpdateGuestSessionStateResultCodes.SessionEnded, reponse.ResultCode);
        }

        [Fact]
        public async Task UpdateGuestSessionStateAsync_WhenUpdatingAnEndedSession_ReturnsSameAsCurrent()
        {
            _defaultGuestSession.GuestSessionState = GuestState.InLobby;
            _updateGuestSessionStateRequest.GuestSessionState = _defaultGuestSession.GuestSessionState;

            var reponse = await _target.UpdateGuestSessionStateAsync(_updateGuestSessionStateRequest, Guid.NewGuid());

            Assert.Equal(UpdateGuestSessionStateResultCodes.SameAsCurrent, reponse.ResultCode);
        }

        [Fact]
        public async Task UpdateGuestSessionStateAsync_IfProjectNotFound_ReturnsFailed()
        {
            _defaultGuestSession.GuestSessionState = GuestState.InProject;
            _serviceToServiceProjectApiMock
                .Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<KeyValuePair<string, string>>>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.NotFound, new Project()));

            var reponse = await _target.UpdateGuestSessionStateAsync(_updateGuestSessionStateRequest, Guid.NewGuid());

            _serviceToServiceProjectApiMock.Verify(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<KeyValuePair<string, string>>>()));
            Assert.Equal(UpdateGuestSessionStateResultCodes.Failed, reponse.ResultCode);
        }

        [Fact]
        public async Task UpdateGuestSessionStateAsync_IfGetProjectByIdFails_ReturnsFailed()
        {
            _defaultGuestSession.GuestSessionState = GuestState.InProject;
            _serviceToServiceProjectApiMock
                .Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<KeyValuePair<string, string>>>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.BadRequest, new Project()));

            var reponse = await _target.UpdateGuestSessionStateAsync(_updateGuestSessionStateRequest, Guid.NewGuid());

            _serviceToServiceProjectApiMock.Verify(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<KeyValuePair<string, string>>>()));
            Assert.Equal(UpdateGuestSessionStateResultCodes.Failed, reponse.ResultCode);
        }

        [Fact]
        public async Task UpdateGuestSessionStateAsync_IfGuestAccessCodeChangedAndSessionEnded_ReturnsSessionEnded()
        {
            _updateGuestSessionStateRequest.GuestSessionState = GuestState.InProject;
            _defaultGuestSession.ProjectAccessCode = Guid.NewGuid().ToString();

            var reponse = await _target.UpdateGuestSessionStateAsync(_updateGuestSessionStateRequest, Guid.NewGuid());

            Assert.Equal(UpdateGuestSessionStateResultCodes.SessionEnded, reponse.ResultCode);
        }

        private GuestSession MakeSession()
        {
            var session = GuestSession.Example();
            return session;
        }

        [Fact]
        public async Task UpdateGuestSessionStateAsync_ProjectAlreadyFull_ReturnsProjectFull()
        {
            SetupGuests(GuestSessionController.GuestSessionLimit);
            _defaultGuestSession.ProjectAccessCode = Guid.NewGuid().ToString();
            _defaultProject.GuestAccessCode = _defaultGuestSession.ProjectAccessCode;
            _updateGuestSessionStateRequest.GuestSessionState = GuestState.InProject;

            var reponse = await _target.UpdateGuestSessionStateAsync(_updateGuestSessionStateRequest, Guid.NewGuid());

            Assert.Equal(UpdateGuestSessionStateResultCodes.ProjectFull, reponse.ResultCode);
        }

        [Fact]
        public async Task UpdateGuestSessionStateAsync_ProjectAlreadyFull_SetsProjectLobbyToGuestLimitReached()
        {
            SetupGuests(GuestSessionController.GuestSessionLimit);
            _defaultGuestSession.ProjectAccessCode = Guid.NewGuid().ToString();
            _defaultProject.GuestAccessCode = _defaultGuestSession.ProjectAccessCode;
            _updateGuestSessionStateRequest.GuestSessionState = GuestState.InProject;

            await _target.UpdateGuestSessionStateAsync(_updateGuestSessionStateRequest, Guid.NewGuid());

            _projectLobbyStateControllerMock.Verify(x => x.UpsertProjectLobbyStateAsync(It.IsAny<Guid>(), It.Is<ProjectLobbyState>(state => state.LobbyState == LobbyState.GuestLimitReached)));
        }

        [Fact]
        public async Task UpdateGuestSessionStateAsync_WithValidRequest_UpdatesGuestSession()
        {
            _defaultGuestSession.ProjectAccessCode = Guid.NewGuid().ToString();
            _defaultProject.GuestAccessCode = _defaultGuestSession.ProjectAccessCode;
            _updateGuestSessionStateRequest.GuestSessionState = GuestState.InProject;

            await _target.UpdateGuestSessionStateAsync(_updateGuestSessionStateRequest, Guid.NewGuid());

            _guestSessionRepositoryMock.Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestSession>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
        }

        [Fact]
        public async Task UpdateGuestSessionStateAsync_ProjectReachedFull_SetsProjectLobbyToGuestLimitReached()
        {
            SetupGuests(GuestSessionController.GuestSessionLimit-1);
            _defaultGuestSession.ProjectAccessCode = Guid.NewGuid().ToString();
            _defaultProject.GuestAccessCode = _defaultGuestSession.ProjectAccessCode;
            _updateGuestSessionStateRequest.GuestSessionState = GuestState.InProject;

            await _target.UpdateGuestSessionStateAsync(_updateGuestSessionStateRequest, Guid.NewGuid());

            _guestSessionRepositoryMock.Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestSession>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
            _projectLobbyStateControllerMock.Verify(x => x.UpsertProjectLobbyStateAsync(It.IsAny<Guid>(), It.Is<ProjectLobbyState>(state => state.LobbyState == LobbyState.GuestLimitReached)));
        }

        [Fact]
        public async Task UpdateGuestSessionStateAsync_ProjectReachedFull_SetsProjectLobbyToNormal()
        {
            SetupGuests(GuestSessionController.GuestSessionLimit);
            _defaultGuestSession.ProjectAccessCode = Guid.NewGuid().ToString();
            _defaultGuestSession.GuestSessionState = GuestState.InProject;
            _defaultProject.GuestAccessCode = _defaultGuestSession.ProjectAccessCode;
            _updateGuestSessionStateRequest.GuestSessionState = GuestState.PromotedToProjectMember;

            await _target.UpdateGuestSessionStateAsync(_updateGuestSessionStateRequest, Guid.NewGuid());

            _guestSessionRepositoryMock.Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestSession>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
            _projectLobbyStateControllerMock.Verify(x => x.UpsertProjectLobbyStateAsync(It.IsAny<Guid>(), It.Is<ProjectLobbyState>(state => state.LobbyState == LobbyState.Normal)));
        }

        [Fact]
        public async Task UpdateGuestSessionStateAsync_WithValidRequest_ReturnsSuccess()
        {
            _defaultGuestSession.ProjectAccessCode = Guid.NewGuid().ToString();
            _defaultGuestSession.GuestSessionState = GuestState.InProject;
            _defaultProject.GuestAccessCode = _defaultGuestSession.ProjectAccessCode;
            _updateGuestSessionStateRequest.GuestSessionState = GuestState.PromotedToProjectMember;

            var result = await _target.UpdateGuestSessionStateAsync(_updateGuestSessionStateRequest, Guid.NewGuid());

            Assert.Equal(UpdateGuestSessionStateResultCodes.Success, result.ResultCode);
        }

        private void SetupGuests(int guestCount)
        {
            var sessionList = new List<GuestSession>();
            Enumerable.Range(0, guestCount).ForEach(arg => sessionList.Add(MakeSession()));
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sessionList);
        }
        #endregion

        #region GetAvailableGuestCountAsync
        #endregion

        #region VerifyGuestAsync

        [Fact]
        public async Task VerifyGuestAsync_WithInvalidGuestVerificationRequest_ThrowsValidationException()
        {
            _validatorMock.Setup(m => m.Validate(It.IsAny<object>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            await Assert.ThrowsAsync<ValidationFailedException>(() => _target.VerifyGuestAsync(_defaultGuestVerificationRequest, _defaultProject, null));
        }

        [Fact]
        public async Task VerifyGuestAsync_IfProjectIdDoesNotMatchRequest_ReturnsInvalidCode()
        {
            _defaultGuestVerificationRequest.ProjectId = Guid.NewGuid();

            var result = await _target.VerifyGuestAsync(_defaultGuestVerificationRequest, _defaultProject, null);

            Assert.Equal(VerifyGuestResponseCode.InvalidCode, result.ResultCode);
        }

        [Fact]
        public async Task VerifyGuestAsync_IfProjectAccessCodeDoesNotMatch_ReturnsInvalidCodeForUserInAnotherTenant()
        {
            _defaultGuestVerificationRequest.ProjectAccessCode = Guid.NewGuid().ToString();

            var result = await _target.VerifyGuestAsync(_defaultGuestVerificationRequest, _defaultProject, null);

            Assert.Equal(VerifyGuestResponseCode.InvalidCode, result.ResultCode);
        }

        [Fact]
        public async Task VerifyGuestAsync_IfProjectAccessCodeDoesNotMatch_ReturnsSuccessForUserInSameTenant()
        {
            _defaultGuestVerificationRequest.ProjectAccessCode = Guid.NewGuid().ToString();
            var guestTenantId = _defaultProject.TenantId;

            var result = await _target.VerifyGuestAsync(_defaultGuestVerificationRequest, _defaultProject, guestTenantId);

            Assert.Equal(VerifyGuestResponseCode.Success, result.ResultCode);
        }

        public class ProjectIdData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[] { Guid.Empty };
                yield return new object[] { Guid.NewGuid() };
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        [Theory]
        [ClassData(typeof(ProjectIdData))]
        public async Task VerifyGuestAsync_IfCannotGetProject_ReturnsInvalidCode(Guid projectId)
        {
            _serviceToServiceProjectApiMock
                .Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<KeyValuePair<string, string>>>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.BadRequest, default(Project)));
            _defaultGuestVerificationRequest.ProjectId = projectId;

            var response = await _target.VerifyGuestAsync(_defaultGuestVerificationRequest, null, null);

            Assert.Equal(VerifyGuestResponseCode.InvalidCode, response.ResultCode);
        }

        [Fact]
        public async Task VerifyGuestAsync_ForNullProjectPassed_GetsProjectByIdIfIdSupplied()
        {
            await _target.VerifyGuestAsync(_defaultGuestVerificationRequest, null, null);

            _serviceToServiceProjectApiMock.Verify(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<KeyValuePair<string, string>>>()));
        }

        [Fact]
        public async Task VerifyGuestAsync_ForNullProjectPassedIn_GetsProjectByAccessCodeIfIdNotSupplied()
        {
            _defaultGuestVerificationRequest.ProjectId = Guid.Empty;

            await _target.VerifyGuestAsync(_defaultGuestVerificationRequest, null, null);

            _serviceToServiceProjectApiMock.Verify(x => x.GetProjectByAccessCodeAsync(It.IsAny<string>(), It.IsAny<IEnumerable<KeyValuePair<string, string>>>()));
        }

        [Fact]
        public async Task VerifyGuestAsync_ForEmptyTenant_ReturnsInvalidCode()
        {
            _defaultProject.TenantId = Guid.Empty;

            var response = await _target.VerifyGuestAsync(_defaultGuestVerificationRequest, null, null);

            Assert.Equal(VerifyGuestResponseCode.InvalidCode, response.ResultCode);
        }

        [Fact]
        public async Task VerifyGuestAsync_GetsUser()
        {
            await _target.VerifyGuestAsync(_defaultGuestVerificationRequest, _defaultProject, _defaultProject.TenantId);

            _userApiMock.Verify(x => x.GetUserByUserNameOrEmailAsync(It.IsAny<string>()));
        }

        [Fact]
        public async Task VerifyGuestAsync_IfInvitedToProject_ReturnsSuccessNoUser()
        {
            _userApiMock.Setup(x => x.GetUserByUserNameOrEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.NotFound, default(User)));
            _defaultGuestInvite.GuestEmail = "example@abc.xyz";

            var response = await _target.VerifyGuestAsync(_defaultGuestVerificationRequest, _defaultProject, _defaultProject.TenantId);

            Assert.Equal(VerifyGuestResponseCode.SuccessNoUser, response.ResultCode);
        }

        [Fact]
        public async Task VerifyGuestAsync_IfNotInvitedToProject_ReturnsInvalidNoInvite()
        {
            _userApiMock.Setup(x => x.GetUserByUserNameOrEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.NotFound, default(User)));
            _guestInviteRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(default(GuestInvite));

            var response = await _target.VerifyGuestAsync(_defaultGuestVerificationRequest, _defaultProject, _defaultProject.TenantId);

            Assert.Equal(VerifyGuestResponseCode.InvalidNoInvite, response.ResultCode);
        }

        [Fact]
        public async Task VerifyGuestAsync_IfUserIsLocked_ReturnsUserIsLocked()
        {
            _defaultUser.IsLocked = true;
            var response = await _target.VerifyGuestAsync(_defaultGuestVerificationRequest, _defaultProject, _defaultProject.TenantId);

            Assert.Equal(VerifyGuestResponseCode.UserIsLocked, response.ResultCode);
        }

        [Fact]
        public async Task VerifyGuestAsync_IfUserEmailNotVerified_ReturnsEmailVerificationNeeded()
        {
            _defaultUser.IsEmailVerified = false;
            var response = await _target.VerifyGuestAsync(_defaultGuestVerificationRequest, _defaultProject, _defaultProject.TenantId);

            Assert.Equal(VerifyGuestResponseCode.EmailVerificationNeeded, response.ResultCode);
        }

        [Fact]
        public async Task VerifyGuestAsync_IfGuestModeOffGlobally_ReturnsFailed()
        {
            _settingsApiMock.Setup(x => x.GetUserSettingsAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new UserSettings { IsGuestModeEnabled = false }));

            var response = await _target.VerifyGuestAsync(_defaultGuestVerificationRequest, _defaultProject, Guid.NewGuid());

            Assert.Equal(VerifyGuestResponseCode.Failed, response.ResultCode);
        }

        [Fact]
        public async Task VerifyGuestAsync_IfGuestModeOffInProject_ReturnsFailed()
        {
            _defaultProject.IsGuestModeEnabled = false;

            var response = await _target.VerifyGuestAsync(_defaultGuestVerificationRequest, _defaultProject, Guid.NewGuid());

            Assert.Equal(VerifyGuestResponseCode.Failed, response.ResultCode);
        }

        [Fact]
        public async Task VerifyGuestAsync_ForValidGuestUser_ReturnsSuccess()
        {
            var response = await _target.VerifyGuestAsync(_defaultGuestVerificationRequest, _defaultProject, Guid.NewGuid());

            Assert.Equal(VerifyGuestResponseCode.Success, response.ResultCode);
        }
        #endregion

        [Fact]
        public async Task CreateGuestSession_CallsCreate()
        {
            await _target.CreateGuestSessionAsync(_defaultGuestSession, _defaultPrincipalId, _defaultTenantId);
            _guestSessionRepositoryMock.Verify(x => x.CreateItemAsync(It.IsAny<GuestSession>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task CreateGuestSession_DoesNotGetTenantIdFromProjectServiceWhenTenantIsSupplied()
        {
            await _target.CreateGuestSessionAsync(_defaultGuestSession, _defaultPrincipalId, Guid.NewGuid());
            _serviceToServiceProjectApiMock.Verify(x => x.GetProjectByIdAsync(_defaultGuestSession.ProjectId, It.IsAny<IEnumerable<KeyValuePair<string, string>>>()), Times.Never);
        }

        [Fact]
        public async Task CreateGuestSession_GetsTenantIdFromProjectServiceWhenSuppliedTenantIsEmpty()
        {
            await _target.CreateGuestSessionAsync(_defaultGuestSession, _defaultPrincipalId, Guid.Empty);
            _serviceToServiceProjectApiMock.Verify(x => x.GetProjectByIdAsync(_defaultGuestSession.ProjectId, It.IsAny<IEnumerable<KeyValuePair<string, string>>>()));
        }

        [Fact]
        public async Task CreateGuestSession_PersistsTenantIdFromProjectService()
        {
            var theTenantId = Guid.NewGuid();

            _serviceToServiceProjectApiMock
                .Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<KeyValuePair<string, string>>>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new Project { TenantId = theTenantId }));

            await _target.CreateGuestSessionAsync(_defaultGuestSession, _defaultPrincipalId, Guid.Empty);

            _guestSessionRepositoryMock.Verify(x => x.CreateItemAsync(It.Is<GuestSession>(gs => gs.ProjectTenantId == theTenantId), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task CreateGuestSession_CallsDeleteItemsToClearOldGuestSessionsForUserAndProject()
        {
            await _target.CreateGuestSessionAsync(_defaultGuestSession, _defaultPrincipalId, _defaultTenantId);

            _guestSessionRepositoryMock.Verify(x => x.DeleteItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task CreateGuestSession_DeletesProjectGuestContextKeysForExistingGuestSessionsForUser()
        {
            var userId = Guid.NewGuid();

            var newGuestSession = new GuestSession
            {
                Id = Guid.NewGuid(),
                ProjectId = Guid.NewGuid(),
                UserId = userId
            };

            var existingGuestSession1 = new GuestSession
            {
                Id = Guid.NewGuid(),
                ProjectId = Guid.NewGuid(),
                UserId = userId,
                GuestSessionState = GuestState.InProject
            };
            var existingGuestSession2 = new GuestSession
            {
                Id = Guid.NewGuid(),
                ProjectId = Guid.NewGuid(),
                UserId = userId,
                GuestSessionState = GuestState.InLobby
            };

            var guestSessions = new List<GuestSession> { existingGuestSession1, existingGuestSession2 };

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .Returns<Expression<Func<GuestSession, bool>>, BatchOptions, CancellationToken>((predicate, bo, ct) =>
                {
                    var expression = predicate.Compile();
                    IEnumerable<GuestSession> sublist = guestSessions.Where(expression).ToList();
                    return Task.FromResult(sublist);
                });

            await _target.CreateGuestSessionAsync(newGuestSession, _defaultPrincipalId, _defaultTenantId);

            _projectGuestContextServiceMock.Verify(x => x.RemoveProjectGuestContextAsync(It.IsAny<string>()), Times.Exactly(2));
        }

        [Fact]
        public async Task CreateGuestSession_ReturnsProvidedGuestSession()
        {
            var result = await _target.CreateGuestSessionAsync(_defaultGuestSession, _defaultPrincipalId, _defaultTenantId);
            Assert.NotNull(result);
            Assert.Equal(_defaultGuestSession.Id, result.Id);
            Assert.Equal(_defaultGuestSession.UserId, result.UserId);
            Assert.Equal(_defaultGuestSession.ProjectId, result.ProjectId);
            Assert.Equal(_defaultGuestSession.ProjectAccessCode, result.ProjectAccessCode);
        }

        [Fact]
        public async Task CreateGuestSession_CallsRepositoryCreateItemAsync()
        {
            await _target.CreateGuestSessionAsync(_defaultGuestSession, _defaultPrincipalId, _defaultTenantId);
            _guestSessionRepositoryMock.Verify(x => x.CreateItemAsync(It.IsAny<GuestSession>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task CreateNewGuestSession_BussesEvent()
        {
            await _target.CreateGuestSessionAsync(_defaultGuestSession, _defaultPrincipalId, _defaultTenantId);
            _eventServiceMock.Verify(x => x.PublishAsync(It.IsAny<ServiceBusEvent<GuestSession>>()));
        }

        [Fact]
        public async Task CreateNewGuestSession_SetsProjectAccessCode()
        {
            var result = await _target.CreateGuestSessionAsync(_defaultGuestSession, _defaultPrincipalId, _defaultTenantId);
            Assert.NotNull(result);
            Assert.NotEqual(string.Empty, result.ProjectAccessCode);
            Assert.Equal(_defaultGuestSession.ProjectAccessCode, result.ProjectAccessCode);
        }

        [Fact]
        public async Task CreateNewGuestSession_SetsProjectId()
        {
            var result = await _target.CreateGuestSessionAsync(_defaultGuestSession, _defaultPrincipalId, _defaultTenantId);
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.ProjectId);
            Assert.Equal(_defaultGuestSession.ProjectId, result.ProjectId);
        }

        [Fact]
        public async Task CreateGuestSession_WhenUpdatingExistingGuestSessionsThrowsException_ThrowsException()
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

            await Assert.ThrowsAnyAsync<Exception>(async () => await _target.CreateGuestSessionAsync(guestSession, _defaultPrincipalId, _defaultTenantId));
        }

        [Fact]
        public async Task EndGuestSessionsForProjectAsync_KillsAllActiveSessions()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession>
                {
                    new GuestSession { GuestSessionState = GuestState.InLobby },
                    new GuestSession { GuestSessionState = GuestState.InProject },
                    new GuestSession { GuestSessionState = GuestState.Ended }
                });

            await _target.EndGuestSessionsForProjectAsync(_defaultGuestSession.ProjectId, _defaultGuestInvite.UserId, false);
            _guestSessionRepositoryMock.Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestSession>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task EndGuestSessionsForProjectAsync_KillsInProjectSessions()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession>
                {
                    new GuestSession { GuestSessionState = GuestState.InLobby },
                    new GuestSession { GuestSessionState = GuestState.InProject },
                    new GuestSession { GuestSessionState = GuestState.Ended }
                });

            await _target.EndGuestSessionsForProjectAsync(_defaultGuestSession.ProjectId, _defaultGuestInvite.UserId, true);
            _guestSessionRepositoryMock.Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestSession>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task EndGuestSessionsForProjectAsync_DeletesProjectGuestContextKeysForSessions()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession>
                {
                    new GuestSession { GuestSessionState = GuestState.InLobby },
                    new GuestSession { GuestSessionState = GuestState.InProject }
                });

            await _target.EndGuestSessionsForProjectAsync(_defaultGuestSession.ProjectId, _defaultGuestInvite.UserId, false);

            _projectGuestContextServiceMock.Verify(x => x.RemoveProjectGuestContextAsync(It.IsAny<string>()), Times.Exactly(2));
        }

        [Fact]
        public async Task EndGuestSessionsForProjectAsync_CalculatesProjectLobbyState()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession>
                {
                    new GuestSession { GuestSessionState = GuestState.InLobby },
                    new GuestSession { GuestSessionState = GuestState.InProject },
                    new GuestSession { GuestSessionState = GuestState.Ended }
                });

            await _target.EndGuestSessionsForProjectAsync(_defaultGuestSession.ProjectId, _defaultGuestInvite.UserId, true);
            _projectLobbyStateControllerMock.Verify(x => x.RecalculateProjectLobbyStateAsync(It.IsAny<Guid>()), Times.Once);
        }

        [Fact]
        public async Task EndGuestSessionsForProjectAsync_PublishesGuestSessionsForProjectDeleted()
        {
            var session = GuestSession.Example();
            session.GuestSessionState = GuestState.InLobby;

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession> { session });

            await _target.EndGuestSessionsForProjectAsync(_defaultGuestSession.ProjectId, _defaultGuestInvite.UserId, false);

            _eventServiceMock.Verify(x => x.PublishAsync(It.Is<ServiceBusEvent<GuidEvent>>(y => y.Name == EventNames.GuestSessionsForProjectDeleted)));
        }

        [Fact]
        public async Task EndGuestSessionsForProjectAsync_PublishesProjectStatusUpdated()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession>());

            await _target.EndGuestSessionsForProjectAsync(_defaultGuestSession.ProjectId, _defaultGuestInvite.UserId, false);

            _eventServiceMock.Verify(x => x.PublishAsync(It.Is<ServiceBusEvent<ProjectLobbyState>>(y => y.Name == EventNames.ProjectStatusUpdated)));
        }

        [Fact]
        public async Task DeleteGuestSessionAsync_WithInvalidGuestSessionId_ThrowsValidationException()
        {
            _validatorMock.Setup(v => v.Validate(It.IsAny<Guid>()))
                .Returns(FailedValidationResult);

            await Assert.ThrowsAsync<ValidationFailedException>(async () => await _target.DeleteGuestSessionAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task DeleteGuestSessionAsync_GuestSessionDoesNotExist_DoesNotThrowNotFoundException()
        {
            _guestSessionRepositoryMock.Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DocumentNotFoundException());

            var ex = await Record.ExceptionAsync(async () => await _target.DeleteGuestSessionAsync(Guid.NewGuid()));

            Assert.Null(ex);
        }

        [Fact]
        public async Task DeleteGuestSessionAsync_RemovesProjectGuestContext()
        {
            await _target.DeleteGuestSessionAsync(Guid.NewGuid());

            _projectGuestContextServiceMock.Verify(x => x.RemoveProjectGuestContextAsync(It.IsAny<string>()));
        }

        [Fact]
        public async Task DeleteGuestSessionAsync_DeletesSession()
        {
            await _target.DeleteGuestSessionAsync(Guid.NewGuid());

            _guestSessionRepositoryMock.Verify(x => x.DeleteItemAsync(It.IsAny<Guid>(), It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task DeleteGuestSessionAsync_PublishesSessionDeleted()
        {
            await _target.DeleteGuestSessionAsync(Guid.NewGuid());

            _eventServiceMock.Verify(x => x.PublishAsync(It.IsAny<ServiceBusEvent<GuidEvent>>()));
        }

        [Fact]
        public async Task DeleteGuestSessionAsync_RecalculatesProjectLobbyState()
        {
            await _target.DeleteGuestSessionAsync(Guid.NewGuid());

            _projectLobbyStateControllerMock.Verify(x => x.RecalculateProjectLobbyStateAsync(It.IsAny<Guid>()));
        }

        [Fact]
        public async Task EmailHost_WhenGuestSessionThrowsNotFound_ThrowsNotFoundException()
        {
            _userApiMock
                .Setup(x => x.GetUserAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, User.Example()));

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new NotFoundException("GuestSession could not be found."));

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.EmailHostAsync(_defaultGuestSession.ProjectAccessCode, Guid.NewGuid()));
        }

        [Fact]
        public async Task EmailHost_WhenGetProjectThrowsNotFound_ThrowsNotFoundException()
        {
            _userApiMock
                .Setup(x => x.GetUserAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, User.Example()));

            _serviceToServiceProjectApiMock
                .Setup(x => x.GetProjectByAccessCodeAsync(It.IsAny<string>(), null))
                .ThrowsAsync(new NotFoundException("Project could not be found"));

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.EmailHostAsync(_defaultGuestSession.ProjectAccessCode, Guid.NewGuid()));
        }

        [Fact]
        public async Task EmailHost_WhenGetUserThrowsNotFound_ThrowsNotFoundException()
        {
            _userApiMock
                .Setup(x => x.GetUserAsync(It.IsAny<Guid>()))
                .ThrowsAsync(new NotFoundException("The sending user could not be found"));

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.EmailHostAsync(_defaultGuestSession.ProjectAccessCode, Guid.NewGuid()));
        }

        [Fact]
        public async Task GetGuestSession_CallsRespositoryGetItem()
        {
            var id = Guid.NewGuid();
            await _target.GetGuestSessionAsync(id);
            _guestSessionRepositoryMock.Verify(x => x.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task GetGuestSession_WhenExists_ReturnsGuestSession()
        {
            var result = await _target.GetGuestSessionAsync(Guid.NewGuid());
            Assert.IsType<GuestSession>(result);
        }

        [Fact]
        public async Task GetGuestSession_OnDocumentNotFound_ThrowsNotFound()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .Throws(new NotFoundException("GuestSession could not be found."));

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.GetGuestSessionAsync(Guid.NewGuid()));
        }

        #region UpdateGuestSessionAsync
        [Fact]
        public async Task UpdateGuestSessionAsync_ForInvalidPrincipalId_ThrowsValidationFailedException()
        {
            _validatorLocator.Setup(m => m.GetValidator(typeof(GuestSessionIdValidator)))
                .Returns(_validatorFailsMock.Object);

            await Assert.ThrowsAsync<ValidationFailedException>(async () => await _target.UpdateGuestSessionAsync(_defaultGuestSession, Guid.Empty));
        }

        [Fact]
        public async Task UpdateGuestSessionAsync_ForInvalidGuestSession_ThrowsValidationFailedException()
        {
            _validatorLocator.Setup(m => m.GetValidator(typeof(GuestSessionValidator)))
                .Returns(_validatorFailsMock.Object);

            await Assert.ThrowsAsync<ValidationFailedException>(async () => await _target.UpdateGuestSessionAsync(_defaultGuestSession, Guid.Empty));
        }

        [Fact]
        public async Task UpdateGuestSessionAsync_OnNotFoundException_ThrowsNotFound()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.UpdateItemAsync(It.IsAny<Guid>(), _defaultGuestSession, It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .Throws(new NotFoundException("Message"));

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.UpdateGuestSessionAsync(_defaultGuestSession, It.IsAny<Guid>()));
        }

        [Fact]
        public async Task UpdateGuestSessionAsync_WithValidRequest_UpdatesItem()
        {
            await _target.UpdateGuestSessionAsync(_defaultGuestSession, It.IsAny<Guid>());

            _guestSessionRepositoryMock.Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestSession>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task UpdateGuestSessionAsync_WhenEndingSession_SetsAccessRevokedInfo()
        {
            _defaultGuestSession.GuestSessionState = GuestState.Ended;
            _defaultGuestSession.AccessRevokedDateTime = null;
            _defaultGuestSession.AccessRevokedBy = null;

            await _target.UpdateGuestSessionAsync(_defaultGuestSession, It.IsAny<Guid>());

            _guestSessionRepositoryMock.Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.Is<GuestSession>(session => session.AccessRevokedBy != null && session.AccessRevokedDateTime != null), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task UpdateGuestSessionAsync_WhenEnteringProject_SetsAccessGrantedInfo()
        {
            _defaultGuestSession.GuestSessionState = GuestState.InProject;
            _defaultGuestSession.AccessGrantedDateTime = null;
            _defaultGuestSession.AccessGrantedBy = null;

            await _target.UpdateGuestSessionAsync(_defaultGuestSession, It.IsAny<Guid>());

            _guestSessionRepositoryMock.Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.Is<GuestSession>(session => session.AccessGrantedBy != null && session.AccessGrantedDateTime != null), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task UpdateGuestSessionAsync_EndingGuestSession_RemovesProjectGuestContext()
        {
            _defaultGuestSession.GuestSessionState = GuestState.Ended;

            await _target.UpdateGuestSessionAsync(_defaultGuestSession, It.IsAny<Guid>());

            _projectGuestContextServiceMock.Verify(x => x.RemoveProjectGuestContextAsync(It.IsAny<string>()));
        }

        [Fact]
        public async Task UpdateGuestSessionAsync_SendsEvent()
        {
            await _target.UpdateGuestSessionAsync(_defaultGuestSession, It.IsAny<Guid>());

            _eventServiceMock.Verify(x => x.PublishAsync(It.IsAny<ServiceBusEvent<GuestSession>>()));
        }

        [Fact]
        public async Task UpdateGuestSessionAsync_UpdatesProjectGuestContextInRedis()
        {
            await _target.UpdateGuestSessionAsync(_defaultGuestSession, Guid.NewGuid());
            _projectGuestContextServiceMock.Verify(x => x.SetProjectGuestContextAsync(It.IsAny<ProjectGuestContext>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task UpdateGuestSessionAsync_DoesNotUpdateSuppliedSessionTenantId()
        {
            var updatedTenantId = Guid.NewGuid();
            var existingTenantId = Guid.NewGuid();

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>(), It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GuestSession { ProjectTenantId = existingTenantId });

            await _target.UpdateGuestSessionAsync(new GuestSession() { ProjectTenantId = updatedTenantId }, Guid.NewGuid());

            _guestSessionRepositoryMock  // Verifies the update uses the existing and NOT supplied tenantId
                .Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.Is<GuestSession>(gs => gs.ProjectTenantId == existingTenantId),
                    It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()));
        }

        #endregion

        [Fact]
        public async Task UpdateGuestSessionState_IfProjectWithInvalidGuestAccessCodeIsReturned_ThrowsValidationException()
        {
            _validatorMock
                .Setup(v => v.Validate(It.IsAny<object>()))
                .Returns(FailedValidationResult);

            _serviceToServiceProjectApiMock.Setup(x => x.GetProjectByIdAsync(_defaultGuestSession.ProjectId, null))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new Project
                {
                    Id = _defaultGuestSession.ProjectId,
                    GuestAccessCode = "INVALID"
                }));

            await Assert.ThrowsAsync<ValidationFailedException>(async () => await _target.UpdateGuestSessionStateAsync(new UpdateGuestSessionStateRequest
            {
                GuestSessionId = _defaultGuestSession.Id,
                GuestSessionState = GuestState.Ended
            }, It.IsAny<Guid>()));
        }

        [Fact]
        public async Task GetMostRecentValidGuestSessionsByProjectId_IfProjectNotFound_ThrowsNotFoundException()
        {
            _serviceToServiceProjectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), null))
                .ReturnsAsync(MicroserviceResponse.Create<Project>(HttpStatusCode.NotFound, new ErrorResponse()));

            await Assert.ThrowsAsync<NotFoundException>(async () =>
                await _target.GetMostRecentValidGuestSessionsByProjectIdAsync(_defaultGuestSession.ProjectId));
        }

        [Fact]
        public async Task GetMostRecentValidGuestSessionsByProjectId_ReturnsSessionsMatchingProjectIdAndAccessCodeAndNotPromotedToProjectMember()
        {
            var expectedReturnedGuestSession = new GuestSession
            {
                ProjectId = _defaultProject.Id,
                ProjectAccessCode = _defaultProject.GuestAccessCode,
                UserId = Guid.NewGuid()
            };

            var guestSessions = new List<GuestSession>
            {
                expectedReturnedGuestSession,
                new GuestSession
                {
                    ProjectId = _defaultProject.Id,
                    ProjectAccessCode = Guid.NewGuid().ToString(),
                    UserId = Guid.NewGuid()
                },
                new GuestSession
                {
                    ProjectId = Guid.NewGuid(),
                    ProjectAccessCode = _defaultProject.GuestAccessCode,
                    UserId = Guid.NewGuid()
                },
                new GuestSession
                {
                    ProjectId = _defaultProject.Id,
                    ProjectAccessCode = _defaultProject.GuestAccessCode,
                    UserId = Guid.NewGuid(),
                    GuestSessionState = GuestState.PromotedToProjectMember
                }
            };

            _guestSessionRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .Returns<Expression<Func<GuestSession, bool>>, BatchOptions, CancellationToken>((predicate, bo, ct) =>
                {
                    var expression = predicate.Compile();
                    IEnumerable<GuestSession> sublist = guestSessions.Where(expression).ToList();
                    return Task.FromResult(sublist);
                });

            var result = await _target.GetMostRecentValidGuestSessionsByProjectIdAsync(_defaultProject.Id);
            var resultList = result.ToList();

            Assert.Single(resultList);
            Assert.Contains(expectedReturnedGuestSession, resultList);
        }

        [Fact]
        public async Task GetMostRecentValidGuestSessionsByProjectId_IfNoSessionsFoundReturns_EmptyList()
        {
            _guestSessionRepositoryMock.Setup(x => x
                .GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession>());

            var result = await _target.GetMostRecentValidGuestSessionsByProjectIdAsync(_defaultGuestSession.ProjectId);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetMostRecentValidGuestSessionsByProjectId_FiltersResultsToReturnMostRecentSessionForEachUniqueUserId()
        {
            var userId1 = Guid.NewGuid();
            var userId2 = Guid.NewGuid();
            var userId3 = Guid.NewGuid();

            var shouldBeReturned = new List<GuestSession>
            {
                new GuestSession { CreatedDateTime = new DateTime(2018, 7, 26), UserId = userId1 },
                new GuestSession { CreatedDateTime = new DateTime(2018, 7, 26), UserId = userId2 },
                new GuestSession { CreatedDateTime = new DateTime(2018, 7, 26), UserId = userId3 }
            };

            var shouldNotBeReturned = new List<GuestSession>
            {
                new GuestSession { CreatedDateTime = new DateTime(2018, 7, 25), UserId = userId1 },
                new GuestSession { CreatedDateTime = new DateTime(2018, 7, 25), UserId = userId2 },
                new GuestSession { CreatedDateTime = new DateTime(2018, 7, 25), UserId = userId3 }
            };

            _serviceToServiceProjectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), null))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, _defaultProject));

            _guestSessionRepositoryMock.Setup(x => x
                .GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(shouldBeReturned.Concat(shouldNotBeReturned));

            var result = await _target.GetMostRecentValidGuestSessionsByProjectIdAsync(_defaultGuestSession.ProjectId);

            Assert.All(shouldBeReturned, session => Assert.Contains(session, result));
            Assert.All(shouldNotBeReturned, session => Assert.DoesNotContain(session, result));
        }

        [Fact]
        public async Task GetValidGuestSessionsByProjectIdForCurrentUser_WhenProjectNotFound_ThrowsNotFoundException()
        {
            _serviceToServiceProjectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), null))
                .ReturnsAsync(MicroserviceResponse.Create<Project>(HttpStatusCode.NotFound, new ErrorResponse()));

            await Assert.ThrowsAsync<NotFoundException>(async () =>
                await _target.GetValidGuestSessionsByProjectIdForCurrentUserAsync(_defaultGuestSession.ProjectId, Guid.NewGuid()));
        }

        [Fact]
        public async Task GetValidGuestSessionsByProjectIdForCurrentUser_WhenNoSessionsFound_ReturnsEmpty()
        {
            _serviceToServiceProjectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), null))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, _defaultProject));

            _guestSessionRepositoryMock.Setup(x => x
                    .GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession>());

            var result = await _target.GetValidGuestSessionsByProjectIdForCurrentUserAsync(_defaultGuestSession.ProjectId, Guid.NewGuid());

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetValidGuestSessionsByProjectIdForCurrentUser_ReturnsItemsMatchingQueryWhereClause()
        {
            var expectedUserId = Guid.NewGuid();

            var guestExpectedGuestSessions = new List<GuestSession>
            {
                new GuestSession
                {
                    ProjectId = _defaultProject.Id,
                    ProjectAccessCode = _defaultProject.GuestAccessCode,
                    UserId = expectedUserId,
                    CreatedDateTime = DateTime.UtcNow
                },
                new GuestSession
                {
                    ProjectId = _defaultProject.Id,
                    ProjectAccessCode = _defaultProject.GuestAccessCode,
                    UserId = expectedUserId,
                    CreatedDateTime = DateTime.UtcNow.AddHours(-2.0)
                },
                new GuestSession
                {
                    ProjectId = _defaultProject.Id,
                    ProjectAccessCode = _defaultProject.GuestAccessCode,
                    UserId = expectedUserId,
                    CreatedDateTime = DateTime.UtcNow.AddDays(-2.0)
                },
            };

            var notExpectedGuestSessions = new List<GuestSession>
            {
                new GuestSession
                {
                    ProjectId = _defaultProject.Id,
                    ProjectAccessCode = Guid.NewGuid().ToString(),
                    UserId = Guid.NewGuid()
                },
                new GuestSession
                {
                    ProjectId = Guid.NewGuid(),
                    ProjectAccessCode = _defaultProject.GuestAccessCode,
                    UserId = Guid.NewGuid()
                },
                new GuestSession
                {
                    ProjectId = _defaultProject.Id,
                    ProjectAccessCode = _defaultProject.GuestAccessCode,
                    UserId = Guid.NewGuid(),
                    GuestSessionState = GuestState.PromotedToProjectMember
                }
            };

            var guestSessions = notExpectedGuestSessions.Concat(guestExpectedGuestSessions).ToList();

            _serviceToServiceProjectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), null))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, _defaultProject));

            _guestSessionRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .Returns<Expression<Func<GuestSession, bool>>, BatchOptions, CancellationToken>((predicate, bo, ct) =>
                {
                    var expression = predicate.Compile();
                    IEnumerable<GuestSession> sublist = guestSessions.Where(expression).ToList();
                    return Task.FromResult(sublist);
                });

            var result = await _target.GetValidGuestSessionsByProjectIdForCurrentUserAsync(_defaultProject.Id, expectedUserId);
            var resultList = result.ToList();

            Assert.Collection(resultList,
                item => Assert.Equal(guestExpectedGuestSessions[0], item),
                item => Assert.Equal(guestExpectedGuestSessions[1], item),
                item => Assert.Equal(guestExpectedGuestSessions[2], item)
            );
        }

        [Fact]
        public async Task GetGuestSessionsByProjectIdForUser_WhenProjectNotFound_ThrowsNotFoundException()
        {
            _serviceToServiceProjectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), null))
                .ReturnsAsync(MicroserviceResponse.Create<Project>(HttpStatusCode.NotFound, new ErrorResponse()));

            await Assert.ThrowsAsync<NotFoundException>(async () =>
                await _target.GetGuestSessionsByProjectIdForUserAsync(_defaultGuestSession.ProjectId, Guid.NewGuid()));
        }

        [Fact]
        public async Task GetGuestSessionsByProjectIdForUser_WhenNoSessionsFound_ReturnsEmpty()
        {
            _serviceToServiceProjectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), null))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, _defaultProject));

            _guestSessionRepositoryMock.Setup(x => x
                    .GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession>());

            var result = await _target.GetGuestSessionsByProjectIdForUserAsync(_defaultGuestSession.ProjectId, Guid.NewGuid());

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetGuestSessionsByProjectIdForUser_ReturnsItemsMatchingQueryWhereClause()
        {
            var expectedUserId = Guid.NewGuid();

            var guestExpectedGuestSessions = new List<GuestSession>
            {
                new GuestSession
                {
                    ProjectId = _defaultProject.Id,
                    ProjectAccessCode = _defaultProject.GuestAccessCode,
                    UserId = expectedUserId,
                    CreatedDateTime = DateTime.UtcNow,
                    GuestSessionState = GuestState.PromotedToProjectMember
                },
                new GuestSession
                {
                    ProjectId = _defaultProject.Id,
                    ProjectAccessCode = _defaultProject.GuestAccessCode,
                    UserId = expectedUserId,
                    CreatedDateTime = DateTime.UtcNow.AddHours(-2.0),
                    GuestSessionState = GuestState.Ended
                },
                new GuestSession
                {
                    ProjectId = _defaultProject.Id,
                    ProjectAccessCode = _defaultProject.GuestAccessCode,
                    UserId = expectedUserId,
                    CreatedDateTime = DateTime.UtcNow.AddDays(-2.0),
                    GuestSessionState = GuestState.Ended
                },
            };

            var notExpectedGuestSessions = new List<GuestSession>
            {
                new GuestSession
                {
                    ProjectId = _defaultProject.Id,
                    ProjectAccessCode = Guid.NewGuid().ToString(),
                    UserId = Guid.NewGuid()
                },
                new GuestSession
                {
                    ProjectId = Guid.NewGuid(),
                    ProjectAccessCode = _defaultProject.GuestAccessCode,
                    UserId = Guid.NewGuid()
                },
                new GuestSession
                {
                    ProjectId = _defaultProject.Id,
                    ProjectAccessCode = _defaultProject.GuestAccessCode,
                    UserId = Guid.NewGuid(),
                    GuestSessionState = GuestState.PromotedToProjectMember
                }
            };

            var guestSessions = notExpectedGuestSessions.Concat(guestExpectedGuestSessions).ToList();

            _serviceToServiceProjectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), null))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, _defaultProject));

            _guestSessionRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .Returns<Expression<Func<GuestSession, bool>>, BatchOptions, CancellationToken>((predicate, bo, ct) =>
                {
                    var expression = predicate.Compile();
                    IEnumerable<GuestSession> sublist = guestSessions.Where(expression).ToList();
                    return Task.FromResult(sublist);
                });

            var result = await _target.GetGuestSessionsByProjectIdForUserAsync(_defaultProject.Id, expectedUserId);
            var resultList = result.ToList();

            Assert.Collection(resultList,
                item => Assert.Equal(guestExpectedGuestSessions[0], item),
                item => Assert.Equal(guestExpectedGuestSessions[1], item),
                item => Assert.Equal(guestExpectedGuestSessions[2], item)
            );
        }
    }
}