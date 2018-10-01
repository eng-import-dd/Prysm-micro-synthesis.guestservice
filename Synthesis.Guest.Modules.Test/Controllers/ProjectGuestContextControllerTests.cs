using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Synthesis.DocumentStorage;
using Synthesis.Guest.ProjectContext.Models;
using Synthesis.Guest.ProjectContext.Services;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.InternalApi.Enums;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.InternalApi.Requests;
using Synthesis.GuestService.InternalApi.Responses;
using Synthesis.Http.Microservice;
using Synthesis.Http.Microservice.Constants;
using Synthesis.Nancy.MicroService;
using Synthesis.PrincipalService.InternalApi.Api;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.ProjectService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Enumerations;
using Synthesis.ProjectService.InternalApi.Models;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Controllers
{
    public class ProjectGuestContextControllerTests
    {
        private readonly Mock<IGuestSessionController> _guestSessionControllerMock = new Mock<IGuestSessionController>();
        private readonly Mock<IProjectLobbyStateController> _projectLobbyStateControllerMock = new Mock<IProjectLobbyStateController>();
        private readonly Mock<IProjectGuestContextService> _projectGuestContextServiceMock = new Mock<IProjectGuestContextService>();
        private readonly Mock<IProjectAccessApi> _projectAccessApiMock = new Mock<IProjectAccessApi>();
        private readonly Mock<IProjectApi> _serviceToServiceProjectApiMock = new Mock<IProjectApi>();
        private readonly Mock<IUserApi> _userApiMock = new Mock<IUserApi>();
        private readonly ProjectGuestContextController _target;
        private readonly Mock<IRepository<GuestSession>> _guestSessionRepositoryMock = new Mock<IRepository<GuestSession>>();
        private readonly Guid _currentUserId = Guid.NewGuid();
        private readonly Guid _projectTenantId = Guid.NewGuid();
        private readonly Guid _noTenantId = Guid.Empty;
        private readonly Guid _defaultProjectId = Guid.NewGuid();
        private readonly Project _defaultProject;
        private readonly List<KeyValuePair<string, string>> _defaultProjectTenantHeaders;
        private readonly ProjectGuestContext _defaultProjectGuestContext;
        private readonly User _defaultUser;
        private readonly string _defaultAccessCode = Guid.NewGuid().ToString();
        private readonly string _defaultUserSessionId = Guid.NewGuid().ToString();
        private readonly GuestSession _defaultGuestSession;

        public ProjectGuestContextControllerTests()
        {
            _defaultProject = new Project() { Id = _defaultProjectId, TenantId = _projectTenantId, GuestAccessCode = _defaultAccessCode };
            _defaultProjectTenantHeaders = new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>(HeaderKeys.Tenant, _defaultProject.TenantId.ToString()) };
            _defaultUser = new User { Id = _currentUserId, Username = "George C" };
            var defaultProjectLobbyState = new ProjectLobbyState { LobbyState = LobbyState.Normal, ProjectId = _defaultProjectId };
            _defaultGuestSession = new GuestSession { Id = Guid.NewGuid(), ProjectId = _defaultProjectId, UserId = _currentUserId, ProjectAccessCode = _defaultAccessCode, GuestSessionState = GuestState.InLobby, SessionId = _defaultUserSessionId };
            _defaultProjectGuestContext = new ProjectGuestContext
            {
                GuestSessionId = _defaultGuestSession.Id,
                GuestState = Guest.ProjectContext.Enums.GuestState.InLobby,
                ProjectId =_defaultProjectId,
                TenantId = _projectTenantId
            };

            var repositoryFactoryMock = new Mock<IRepositoryFactory>();
            repositoryFactoryMock
                .Setup(x => x.CreateRepository<GuestSession>())
                .Returns(_guestSessionRepositoryMock.Object);

            _projectGuestContextServiceMock
                .Setup(x => x.GetProjectGuestContextAsync(It.IsAny<string>()))
                .ReturnsAsync(_defaultProjectGuestContext);

            _projectAccessApiMock
                .Setup(x => x.GrantProjectMembershipAsync(It.IsAny<GrantProjectMembershipRequest>(), _defaultProjectTenantHeaders))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK));

            _guestSessionControllerMock
                .Setup(x => x.UpdateGuestSessionStateAsync(It.IsAny<UpdateGuestSessionStateRequest>(), It.IsAny<Guid>()))
                .ReturnsAsync(new UpdateGuestSessionStateResponse());

            _guestSessionControllerMock
                .Setup(x => x.VerifyGuestAsync(It.IsAny<GuestVerificationRequest>(), It.IsAny<Project>(), It.IsAny<Guid?>()))
                .ReturnsAsync(new GuestVerificationResponse() {ResultCode = VerifyGuestResponseCode.Success});

            _guestSessionControllerMock
                .Setup(x => x.CreateGuestSessionAsync(It.IsAny<GuestSession>()))
                .ReturnsAsync(_defaultGuestSession);

            _userApiMock
                .Setup(x => x.GetUserAsync(_currentUserId))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, _defaultUser));

            _serviceToServiceProjectApiMock
                .Setup(x => x.GetProjectByIdAsync(_defaultProjectId, null))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, _defaultProject));

            _projectAccessApiMock
                .Setup(x => x.GetProjectMemberUserIdsAsync(_defaultProjectId, MemberRoleFilter.FullUser, _defaultProjectTenantHeaders))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, (new List<Guid>() { _currentUserId}).AsEnumerable()));

            _projectLobbyStateControllerMock
                .Setup(x => x.GetProjectLobbyStateAsync(_defaultProjectId))
                .ReturnsAsync(defaultProjectLobbyState);

            _target = new ProjectGuestContextController(repositoryFactoryMock.Object,
                _guestSessionControllerMock.Object,
                _projectLobbyStateControllerMock.Object,
                _projectGuestContextServiceMock.Object,
                _projectAccessApiMock.Object,
                _serviceToServiceProjectApiMock.Object,
                _userApiMock.Object);
        }

        #region Clear Session
        [Fact]
        public async Task SetProjectGuestContext_IfProjectIdIsEmpty_GuestSessionIsEnded()
        {
            await _target.SetProjectGuestContextAsync(Guid.Empty, null, _currentUserId, _noTenantId);

            _guestSessionControllerMock
                .Verify(x => x.UpdateGuestSessionStateAsync(It.Is<UpdateGuestSessionStateRequest>(y =>
                    y.GuestSessionId == _defaultProjectGuestContext.GuestSessionId &&
                    y.GuestSessionState == GuestState.Ended), It.Is<Guid>(p => p == _currentUserId)));
        }

        [Fact]
        public async Task SetProjectGuestContext_IfProjectIdIsEmpty_ProjectGuestContextIsReset()
        {
            await _target.SetProjectGuestContextAsync(Guid.Empty, null, _currentUserId, _noTenantId);

            _projectGuestContextServiceMock
                .Verify(y => y.SetProjectGuestContextAsync(It.Is<ProjectGuestContext>(x =>
                    x.ProjectId == Guid.Empty &&
                    x.GuestSessionId == Guid.Empty), It.IsAny<string>()));
        }

        [Fact]
        public async Task SetProjectGuestContext_IfGuestSessionCannotBeCleared_ThrowsInvalidOperationException()
        {
            _guestSessionControllerMock
                .Setup(x => x.UpdateGuestSessionStateAsync(It.IsAny<UpdateGuestSessionStateRequest>(), It.IsAny<Guid>()))
                .ReturnsAsync(new UpdateGuestSessionStateResponse() { ResultCode = UpdateGuestSessionStateResultCodes.Failed });

            await Assert.ThrowsAsync<InvalidOperationException>(() => _target.SetProjectGuestContextAsync(Guid.Empty, null, _currentUserId, _noTenantId));
        }
        #endregion

        #region LoadCriticalData
        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.BadGateway)]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.Unauthorized)]
        public async Task SetProjectGuestContext_IfProjectCannotBeFetchedByServiceToServiceClientForGuest_ThrowsInvalidOperationException(HttpStatusCode statusCode)
        {
            _serviceToServiceProjectApiMock
                .Setup(x => x.GetProjectByIdAsync(_defaultProjectId, null))
                .ReturnsAsync(MicroserviceResponse.Create(statusCode, default(Project)));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _target.SetProjectGuestContextAsync(_defaultProjectId, null, _currentUserId, _noTenantId));
        }

        [Fact]
        public async Task SetProjectGuestContext_CallsGetProjectById()
        {
            await _target.SetProjectGuestContextAsync(_defaultProjectId, _defaultAccessCode, _currentUserId, _noTenantId);

            _serviceToServiceProjectApiMock
                .Verify(y => y.GetProjectByIdAsync(_defaultProjectId, null));
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task SetProjectGuestContext_IfProjectMembershipCannotBeFetched_ThrowsInvalidOperationException(HttpStatusCode statusCode)
        {
            _projectAccessApiMock
                .Setup(x => x.GetProjectMemberUserIdsAsync(_defaultProjectId, MemberRoleFilter.FullUser, _defaultProjectTenantHeaders))
                .ReturnsAsync(MicroserviceResponse.Create(statusCode, default(IEnumerable<Guid>)));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _target.SetProjectGuestContextAsync(_defaultProjectId, null, _currentUserId, _noTenantId));
        }


        [Fact]
        public async Task SetProjectGuestContext_CallsGetProjectMemberUserids()
        {
            await _target.SetProjectGuestContextAsync(_defaultProjectId, _defaultAccessCode, _currentUserId, _noTenantId);

            _projectAccessApiMock
                .Verify(y => y.GetProjectMemberUserIdsAsync(_defaultProjectId, MemberRoleFilter.FullUser, _defaultProjectTenantHeaders));
        }

        [Fact]
        public async Task SetProjectGuestContext_CallsGetProjectGuestContext()
        {
            await _target.SetProjectGuestContextAsync(_defaultProjectId, _defaultAccessCode, _currentUserId, _noTenantId);

            _projectGuestContextServiceMock
                .Verify(y => y.GetProjectGuestContextAsync(It.IsAny<string>()));
        }
        #endregion LoadCriticalData

        [Fact]
        public async Task SetProjectGuestContext_WhenUserIsNotFullProjectMemberAndRequestingLobbyEntry_UserHasAccessIsFalse()
        {
            _projectGuestContextServiceMock
                .Setup(x => x.GetProjectGuestContextAsync(It.IsAny<string>()))
                .ReturnsAsync(default(ProjectGuestContext));

            _projectAccessApiMock
                .Setup(x => x.GetProjectMemberUserIdsAsync(_defaultProjectId, MemberRoleFilter.FullUser, _defaultProjectTenantHeaders))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, (new List<Guid>()).AsEnumerable()));

            var response = await _target.SetProjectGuestContextAsync(_defaultProjectId, "code", _currentUserId, Guid.NewGuid());

            Assert.False(response.UserHasAccess);
        }

        [Fact]
        public async Task SetProjectGuestContext_WhenUserFromSameTenantIsProjectMemberAndRequestsLobbyEntry_UserHasAccessIsTrue()
        {
            _projectGuestContextServiceMock
                .Setup(x => x.GetProjectGuestContextAsync(It.IsAny<string>()))
                .ReturnsAsync(default(ProjectGuestContext));

            var response = await _target.SetProjectGuestContextAsync(_defaultProjectId, null, _currentUserId, _projectTenantId);

            Assert.True(response.UserHasAccess);
        }

        [Fact]
        public async Task SetProjectGuestContext_IfUserHasDifferentTenantHasBeenAdmittedToProject_UserHasAccessIsTrue()
        {
            var guestSessionId = Guid.NewGuid();

            _projectGuestContextServiceMock
                .Setup(x => x.GetProjectGuestContextAsync(It.IsAny<string>()))
                .ReturnsAsync(new ProjectGuestContext()
                {
                    GuestSessionId = guestSessionId,
                    GuestState = Guest.ProjectContext.Enums.GuestState.InProject,
                    ProjectId = _defaultProjectId,
                    TenantId = _projectTenantId
                });

            _projectAccessApiMock
                .Setup(x => x.GetProjectMemberUserIdsAsync(_defaultProjectId, MemberRoleFilter.FullUser, _defaultProjectTenantHeaders))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, (new List<Guid>()).AsEnumerable()));

            var guestSession = new GuestSession { Id = guestSessionId, ProjectId = _defaultProjectId, UserId = _currentUserId, ProjectAccessCode = _defaultAccessCode, GuestSessionState = GuestState.InProject };
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemAsync(It.Is<Guid>(g => g == guestSessionId), It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(guestSession);

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession> { guestSession });

            var response = await _target.SetProjectGuestContextAsync(_defaultProjectId, "code", _currentUserId, Guid.NewGuid());

            Assert.True(response.UserHasAccess);
        }

        [Fact]
        public async Task SetProjectGuestContext_WhenEvaluatingNonMemberUserCanEnterLobby_CallsGetUser()
        {
            _projectGuestContextServiceMock
                .Setup(x => x.GetProjectGuestContextAsync(It.IsAny<string>()))
                .ReturnsAsync(default(ProjectGuestContext));

            _projectAccessApiMock
                .Setup(x => x.GetProjectMemberUserIdsAsync(_defaultProjectId, MemberRoleFilter.FullUser, _defaultProjectTenantHeaders))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, (new List<Guid>()).AsEnumerable()));

            await _target.SetProjectGuestContextAsync(_defaultProjectId, null, _currentUserId, _projectTenantId);

            _userApiMock.Verify(x => x.GetUserAsync(It.Is<Guid>(id => id == _currentUserId)));
        }

        [Fact]
        public async Task SetProjectGuestContext_WhenEvaluatingNonMemberUserCanEnterLobbyAndGetUserCallReturnsNotOK_ThrowsInvalidOperationException()
        {
            _projectGuestContextServiceMock
                .Setup(x => x.GetProjectGuestContextAsync(It.IsAny<string>()))
                .ReturnsAsync(default(ProjectGuestContext));

            _projectAccessApiMock
                .Setup(x => x.GetProjectMemberUserIdsAsync(_defaultProjectId, MemberRoleFilter.FullUser, _defaultProjectTenantHeaders))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, (new List<Guid>()).AsEnumerable()));

            _userApiMock
                .Setup(x => x.GetUserAsync(It.IsAny<Guid>()))
                .Throws<InvalidOperationException>();

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await _target.SetProjectGuestContextAsync(_defaultProjectId, null, _currentUserId, _projectTenantId));
        }

        [Fact]
        public async Task SetProjectGuestContext_WhenEvaluatingNonMemberUserCanEnterLobby_CallsVerifyGuest()
        {
            _projectGuestContextServiceMock
                .Setup(x => x.GetProjectGuestContextAsync(It.IsAny<string>()))
                .ReturnsAsync(default(ProjectGuestContext));

            _projectAccessApiMock
                .Setup(x => x.GetProjectMemberUserIdsAsync(_defaultProjectId, MemberRoleFilter.FullUser, _defaultProjectTenantHeaders))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, (new List<Guid>()).AsEnumerable()));

            await _target.SetProjectGuestContextAsync(_defaultProjectId, null, _currentUserId, _projectTenantId);

            _guestSessionControllerMock.Verify(x => x.VerifyGuestAsync(It.Is<GuestVerificationRequest>(props =>
                props.ProjectAccessCode == null
                && props.ProjectId == _defaultProjectId
                && props.Username == _defaultUser.Username)
                , It.Is<Project>(p => p == _defaultProject)
                , It.Is<Guid>(t => t == _projectTenantId)));
        }

        [Theory]
        [InlineData(VerifyGuestResponseCode.InvalidCode)]
        [InlineData(VerifyGuestResponseCode.EmailVerificationNeeded)]
        [InlineData(VerifyGuestResponseCode.Failed)]
        [InlineData(VerifyGuestResponseCode.InvalidEmail)]
        [InlineData(VerifyGuestResponseCode.InvalidNoInvite)]
        [InlineData(VerifyGuestResponseCode.UserIsLocked)]
        public async Task SetProjectGuestContext_WhenEvaluatingNonMemberUserCanEnterLobbyAndVerifyGuestResultIsNotSuccess_ThrowsInvalidOperationException(VerifyGuestResponseCode code)
        {
            _projectGuestContextServiceMock
                .Setup(x => x.GetProjectGuestContextAsync(It.IsAny<string>()))
                .ReturnsAsync(default(ProjectGuestContext));

            _projectAccessApiMock
                .Setup(x => x.GetProjectMemberUserIdsAsync(_defaultProjectId, MemberRoleFilter.FullUser, _defaultProjectTenantHeaders))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, (new List<Guid>()).AsEnumerable()));

            _guestSessionControllerMock
                .Setup(x => x.VerifyGuestAsync(It.IsAny<GuestVerificationRequest>(), It.IsAny<Project>(),  _projectTenantId))
                .ReturnsAsync(new GuestVerificationResponse() { ResultCode = code, Username = _defaultUser.Username});

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await _target.SetProjectGuestContextAsync(_defaultProjectId, null, _currentUserId, _projectTenantId));
        }

        [Fact]
        public async Task SetProjectGuestContext_WhenEvaluatingNonMemberUserCanEnterLobbyAndVerifyGuestReturnsSuccess_CallsCreateGuestSessionToPutUserOfficiallInLobby()
        {
            _projectGuestContextServiceMock
                .Setup(x => x.GetProjectGuestContextAsync(It.IsAny<string>()))
                .ReturnsAsync(default(ProjectGuestContext));

            _projectAccessApiMock
                .Setup(x => x.GetProjectMemberUserIdsAsync(_defaultProjectId, MemberRoleFilter.FullUser, _defaultProjectTenantHeaders))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, (new List<Guid>()).AsEnumerable()));

             await _target.SetProjectGuestContextAsync(_defaultProjectId, null, _currentUserId, _projectTenantId);

            _guestSessionControllerMock
                .Verify(x => x.CreateGuestSessionAsync(It.Is<GuestSession>(props => props.UserId == _currentUserId
                && props.ProjectId == _defaultProjectId
                && props.ProjectAccessCode == _defaultProject.GuestAccessCode
                && props.GuestSessionState == GuestState.InLobby)));
        }


        [Fact]
        public async Task SetProjectGuestContext_WhenEvaluatingNonMemberUserCanEnterLobbyAndVerifyGuestReturnsSuccess_CachesProjectGuestContextRecord()
        {
            _projectGuestContextServiceMock
                .Setup(x => x.GetProjectGuestContextAsync(It.IsAny<string>()))
                .ReturnsAsync(default(ProjectGuestContext));

            _projectAccessApiMock
                .Setup(x => x.GetProjectMemberUserIdsAsync(_defaultProjectId, MemberRoleFilter.FullUser, _defaultProjectTenantHeaders))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, (new List<Guid>()).AsEnumerable()));

            await _target.SetProjectGuestContextAsync(_defaultProjectId, null, _currentUserId, _projectTenantId);

            _projectGuestContextServiceMock
                .Verify(x => x.SetProjectGuestContextAsync(It.Is<ProjectGuestContext>(props => props.GuestSessionId == _defaultGuestSession.Id
                    && props.ProjectId == _defaultProjectId
                    && props.TenantId == _defaultProject.TenantId
                    && props.GuestState == Guest.ProjectContext.Enums.GuestState.InLobby), It.IsAny<string>()));
        }

        [Fact]
        public async Task SetProjectGuestContext_WhenEvaluatingNonMemberUserCanEnterLobbyAndVerifyGuestReturnsSuccess_ReturnsCurrentProjectState()
        {
            _projectGuestContextServiceMock
                .SetupSequence(x => x.GetProjectGuestContextAsync(It.IsAny<string>()))
                .ReturnsAsync(default(ProjectGuestContext))
                .ReturnsAsync(_defaultProjectGuestContext);

            _projectAccessApiMock
                .Setup(x => x.GetProjectMemberUserIdsAsync(_defaultProjectId, MemberRoleFilter.FullUser, _defaultProjectTenantHeaders))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, (new List<Guid>()).AsEnumerable()));

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemAsync(It.Is<Guid>(g => g == _defaultGuestSession.Id), It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_defaultGuestSession);

            var result = await _target.SetProjectGuestContextAsync(_defaultProjectId, null, _currentUserId, _projectTenantId);

            Assert.IsAssignableFrom<CurrentProjectState>(result);
            Assert.IsAssignableFrom<GuestSession>(result.GuestSession);
            Assert.False(result.UserHasAccess);
            Assert.Equal(_defaultGuestSession.Id, result.GuestSession.Id);
            Assert.Equal(_defaultAccessCode, result.GuestSession.ProjectAccessCode);
            Assert.Equal(_defaultProjectId, result.GuestSession.ProjectId);
            Assert.Equal(_currentUserId, result.GuestSession.UserId);
            Assert.Equal(GuestState.InLobby, result.GuestSession.GuestSessionState);
            Assert.Equal(_defaultProject, result.Project);
            Assert.Equal(LobbyState.Normal, result.LobbyState);
        }


        [Fact]
        public async Task SetProjectGuestContext_IfUserIsAGuestInTheSameAccountAndWhenAdmittedFromLobbyWasPromotedToMember_ClearsProjectGuestContext()
        {
            var guestSessionId = Guid.NewGuid();

            _projectGuestContextServiceMock
                .Setup(x => x.GetProjectGuestContextAsync(It.IsAny<string>()))
                .ReturnsAsync(new ProjectGuestContext()
                {
                    GuestSessionId = guestSessionId,
                    GuestState = Guest.ProjectContext.Enums.GuestState.PromotedToProjectMember,
                    ProjectId = _defaultProjectId,
                    TenantId = _projectTenantId
                });

            var guestSession = new GuestSession { Id = guestSessionId, ProjectId = _defaultProjectId, UserId = _currentUserId, ProjectAccessCode = _defaultAccessCode, GuestSessionState = GuestState.PromotedToProjectMember };
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemAsync(It.Is<Guid>(g => g == guestSessionId), It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(guestSession);

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession> { guestSession });

            await _target.SetProjectGuestContextAsync(_defaultProjectId, null, _currentUserId, _projectTenantId);

            _projectGuestContextServiceMock
                .Verify(y => y.SetProjectGuestContextAsync(It.Is<ProjectGuestContext>(x =>
                    x.ProjectId == Guid.Empty &&
                    x.GuestSessionId == Guid.Empty), It.IsAny<string>()));
        }

        [Fact]
        public async Task SetProjectGuestContext_WhenUserIsAlreadyInLobby_CallsGetProjectGuestContextToCreateResponse()
        {
            _projectGuestContextServiceMock
                .Setup(x => x.GetProjectGuestContextAsync(It.IsAny<string>()))
                .ReturnsAsync(new ProjectGuestContext()
                {
                    GuestSessionId = Guid.NewGuid(),
                    GuestState = Guest.ProjectContext.Enums.GuestState.InLobby,
                    ProjectId = _defaultProjectId,
                    TenantId = _projectTenantId
                });

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession> { _defaultGuestSession });

            await _target.SetProjectGuestContextAsync(_defaultProjectId, _defaultAccessCode, Guid.NewGuid(), _noTenantId);

            _projectGuestContextServiceMock.Verify(y => y.GetProjectGuestContextAsync(It.IsAny<string>()), Times.Exactly(2));
        }


        [Fact]
        public async Task SetProjectGuestContext_WhenUserIsAlreadyInLobby_CallsGetProjectLobbyStateToCreateResponse()
        {
            _projectGuestContextServiceMock
                .Setup(x => x.GetProjectGuestContextAsync(It.IsAny<string>()))
                .ReturnsAsync(new ProjectGuestContext()
                {
                    GuestSessionId = Guid.NewGuid(),
                    GuestState = Guest.ProjectContext.Enums.GuestState.InLobby,
                    ProjectId = _defaultProjectId,
                    TenantId = _projectTenantId
                });

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession> { _defaultGuestSession });

            await _target.SetProjectGuestContextAsync(_defaultProjectId, _defaultAccessCode, Guid.NewGuid(), _noTenantId);

            _projectLobbyStateControllerMock.Verify(y => y.GetProjectLobbyStateAsync(It.Is<Guid>(id => id == _defaultProjectId)));
        }

        [Fact]
        public async Task SetProjectGuestContext_WhenUserIsAlreadyInLobby_CallsGuestSessionRepositoryGetItemToCreateResponse()
        {
            var guestSessionId = Guid.NewGuid();

            _projectGuestContextServiceMock
                .Setup(x => x.GetProjectGuestContextAsync(It.IsAny<string>()))
                .ReturnsAsync(new ProjectGuestContext()
                {
                    GuestSessionId = guestSessionId,
                    GuestState = Guest.ProjectContext.Enums.GuestState.InLobby,
                    ProjectId = _defaultProjectId,
                    TenantId = _projectTenantId
                });

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemAsync(guestSessionId, It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync( _defaultGuestSession );

            await _target.SetProjectGuestContextAsync(_defaultProjectId, _defaultAccessCode, Guid.NewGuid(), _noTenantId);

            _guestSessionRepositoryMock.Verify(y => y.GetItemAsync(guestSessionId, It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()));
        }


        [Fact]
        public async Task SetProjectGuestContext_WhenUserIsAlreadyInLobbyAndGetProjectLobbyStateAsyncThrowsNotFound_ThrowsNotFoundException()
        {
            _projectGuestContextServiceMock
                .Setup(x => x.GetProjectGuestContextAsync(It.IsAny<string>()))
                .ReturnsAsync(new ProjectGuestContext()
                {
                    GuestSessionId = Guid.NewGuid(),
                    GuestState = Guest.ProjectContext.Enums.GuestState.InLobby,
                    ProjectId = _defaultProjectId,
                    TenantId = _projectTenantId
                });

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession> { _defaultGuestSession });

            _projectLobbyStateControllerMock
                .Setup(y => y.GetProjectLobbyStateAsync(It.IsAny<Guid>()))
                .Throws<NotFoundException>();

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.SetProjectGuestContextAsync(_defaultProjectId, _defaultAccessCode, Guid.NewGuid(), _noTenantId));
        }

        [Fact]
        public async Task SetProjectGuestContext_WhenNonMemberIsPermittedIntoLobby_IsGrantedGuestMembershipToProject()
        {
            _projectGuestContextServiceMock
                .SetupSequence(x => x.GetProjectGuestContextAsync(It.IsAny<string>()))
                .Returns(Task.FromResult(default(ProjectGuestContext)))
                .Returns(Task.FromResult(_defaultProjectGuestContext));

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>(), It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_defaultGuestSession);

            _projectAccessApiMock
                .Setup(x => x.GetProjectMemberUserIdsAsync(_defaultProjectId, MemberRoleFilter.FullUser, _defaultProjectTenantHeaders))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, (new List<Guid>()).AsEnumerable()));

            await _target.SetProjectGuestContextAsync(_defaultProjectId, _defaultAccessCode, _currentUserId, null);

            _projectAccessApiMock
                .Verify(x => x.GrantProjectMembershipAsync(It.IsAny<GrantProjectMembershipRequest>(), _defaultProjectTenantHeaders));//_currentUserId, _defaultProjectId, _defaultProjectTenantHeaders));
        }

        [Fact]
        public async Task SetProjectGuestContext_WhenNonMemberIsPermittedIntoLobbyAndGrantMembershipCallFails_ThrowsInvalidOperationException()
        {
            _projectGuestContextServiceMock
                .SetupSequence(x => x.GetProjectGuestContextAsync(It.IsAny<string>()))
                .Returns(Task.FromResult(default(ProjectGuestContext)))
                .Returns(Task.FromResult(_defaultProjectGuestContext));

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemAsync(_defaultGuestSession.Id, It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_defaultGuestSession);

            _projectAccessApiMock
                .Setup(x => x.GetProjectMemberUserIdsAsync(_defaultProjectId, MemberRoleFilter.FullUser, _defaultProjectTenantHeaders))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, (new List<Guid>()).AsEnumerable()));

            _projectAccessApiMock
                .Setup(x => x.GrantProjectMembershipAsync(It.IsAny<GrantProjectMembershipRequest>(), _defaultProjectTenantHeaders))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.InternalServerError));

            await Assert.ThrowsAsync<InvalidOperationException>(() => _target.SetProjectGuestContextAsync(_defaultProjectId, _defaultAccessCode, _currentUserId, null));
        }
    }
}