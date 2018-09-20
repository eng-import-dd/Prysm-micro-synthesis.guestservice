using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
        private readonly Guid _userWithTenantTenantId = Guid.NewGuid();
        private readonly Guid _userWithoutTenantTenantId = Guid.Empty;
        private readonly Guid _defaultProjectId = Guid.NewGuid();
        private readonly Project _defaultProject;
        private readonly ProjectGuestContext _defaultProjectGuestContext;
        private readonly ProjectLobbyState _defaultProjectLobbyState;
        private readonly User _defaultUser;
        private readonly string _defaultAccessCode = Guid.NewGuid().ToString();
        private readonly GuestSession _defaultGuestSession;

        public ProjectGuestContextControllerTests()
        {
            _defaultProject = new Project() { Id = _defaultProjectId, TenantId = _userWithTenantTenantId};
            _defaultUser = new User { Id = _currentUserId, Username = "George C" };
            _defaultProjectLobbyState = new ProjectLobbyState() { LobbyState = LobbyState.Normal, ProjectId = _defaultProjectId };
            _defaultGuestSession = new GuestSession { ProjectId = _defaultProjectId, UserId = _currentUserId, ProjectAccessCode = _defaultAccessCode, GuestSessionState = GuestState.InLobby };
            _defaultProjectGuestContext = new ProjectGuestContext()
            {
                GuestSessionId = Guid.NewGuid(),
                GuestState = Synthesis.Guest.ProjectContext.Enums.GuestState.InLobby,
                ProjectId =_defaultProjectId,
                TenantId = _userWithoutTenantTenantId
            };

            var repositoryFactoryMock = new Mock<IRepositoryFactory>();
            repositoryFactoryMock
                .Setup(x => x.CreateRepository<GuestSession>())
                .Returns(_guestSessionRepositoryMock.Object);

            _projectGuestContextServiceMock
                .Setup(x => x.GetProjectGuestContextAsync(It.IsAny<string>()))
                .ReturnsAsync(_defaultProjectGuestContext);

            _projectAccessApiMock
                .Setup(x => x.GrantProjectMembershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IEnumerable<KeyValuePair<string, string>>>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK));

            _guestSessionControllerMock
                .Setup(x => x.UpdateGuestSessionStateAsync(It.IsAny<UpdateGuestSessionStateRequest>(), It.IsAny<Guid>()))
                .ReturnsAsync(new UpdateGuestSessionStateResponse());

            _guestSessionControllerMock
                .Setup(x => x.VerifyGuestAsync(It.IsAny<GuestVerificationRequest>(), It.IsAny<Guid?>()))
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
                .Setup(x => x.GetProjectMemberUserIdsAsync(_defaultProjectId, MemberRoleFilter.FullUser, null))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, (new List<Guid>() { _currentUserId}).AsEnumerable()));

            _projectLobbyStateControllerMock
                .Setup(x => x.GetProjectLobbyStateAsync(_defaultProjectId))
                .ReturnsAsync(_defaultProjectLobbyState);

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
        public async Task GuestSessionIsEndedIfProjectIdIsEmpty()
        {
            await _target.SetProjectGuestContextAsync(Guid.Empty, null, _currentUserId, _userWithoutTenantTenantId);

            _guestSessionControllerMock
                .Verify(x => x.UpdateGuestSessionStateAsync(It.Is<UpdateGuestSessionStateRequest>(y =>
                    y.GuestSessionId == _defaultProjectGuestContext.GuestSessionId &&
                    y.GuestSessionState == GuestState.Ended), It.Is<Guid>(p => p == _currentUserId)));
        }

        [Fact]
        public async Task ProjectGuestContextIsResetIfProjectIdIsEmpty()
        {
            await _target.SetProjectGuestContextAsync(Guid.Empty, null, _currentUserId, _userWithoutTenantTenantId);

            _projectGuestContextServiceMock
                .Verify(y => y.SetProjectGuestContextAsync(It.Is<ProjectGuestContext>(x =>
                    x.ProjectId == Guid.Empty &&
                    x.GuestSessionId == Guid.Empty), It.IsAny<string>()));
        }

        [Fact]
        public async Task InvalidOperationIsThrownIfGuestSessionCannotBeCleared()
        {
            _guestSessionControllerMock
                .Setup(x => x.UpdateGuestSessionStateAsync(It.IsAny<UpdateGuestSessionStateRequest>(), It.IsAny<Guid>()))
                .ReturnsAsync(new UpdateGuestSessionStateResponse() { ResultCode = UpdateGuestSessionStateResultCodes.Failed });

            await Assert.ThrowsAsync<InvalidOperationException>(() => _target.SetProjectGuestContextAsync(Guid.Empty, null, _currentUserId, _userWithoutTenantTenantId));
        }
        #endregion

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task InvalidOperationIsThrownIfProjectCannotBeFetched(HttpStatusCode statusCode)
        {
            _serviceToServiceProjectApiMock
                .Setup(x => x.GetProjectByIdAsync(_defaultProjectId, null))
                .ReturnsAsync(MicroserviceResponse.Create(statusCode, default(Project)));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _target.SetProjectGuestContextAsync(_defaultProjectId, null, _currentUserId, _userWithoutTenantTenantId));
        }

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task InvalidOperationIsThrownIfProjectAccessCannotBeFetched(HttpStatusCode statusCode)
        {
            _projectAccessApiMock
                .Setup(x => x.GetProjectMemberUserIdsAsync(_defaultProjectId, MemberRoleFilter.FullUser, null))
                .ReturnsAsync(MicroserviceResponse.Create(statusCode, default(IEnumerable<Guid>)));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _target.SetProjectGuestContextAsync(_defaultProjectId, null, _currentUserId, _userWithoutTenantTenantId));
        }


        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.BadGateway)]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.Unauthorized)]
        public async Task InvalidOperationIsThrownForGuestIfProjectCannotBeFetchedByServiceToServiceClient(HttpStatusCode statusCode)
        {
            _projectGuestContextServiceMock
                .Setup(x => x.IsGuestAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _serviceToServiceProjectApiMock
                .Setup(x => x.GetProjectByIdAsync(_defaultProjectId, null))
                .ReturnsAsync(MicroserviceResponse.Create(statusCode, default(Project)));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _target.SetProjectGuestContextAsync(_defaultProjectId, null, _currentUserId, _userWithoutTenantTenantId));
        }

        [Fact]
        public async Task ProjectIsClearedIfUserIsAGuestInTheSameAccountAndHasBeenPromoted()
        {
            _projectGuestContextServiceMock
                .Setup(x => x.IsGuestAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            _projectGuestContextServiceMock
                .Setup(x => x.GetProjectGuestContextAsync(It.IsAny<string>()))
                .ReturnsAsync(new ProjectGuestContext()
                {
                    GuestSessionId = Guid.NewGuid(),
                    GuestState = Guest.ProjectContext.Enums.GuestState.InLobby,
                    ProjectId = _defaultProjectId,
                    TenantId = _userWithTenantTenantId
                });

            await _target.SetProjectGuestContextAsync(_defaultProjectId, null, _currentUserId, _userWithTenantTenantId);

            _projectGuestContextServiceMock
                .Verify(y => y.SetProjectGuestContextAsync(It.Is<ProjectGuestContext>(x =>
                    x.ProjectId == Guid.Empty &&
                    x.GuestSessionId == Guid.Empty), It.IsAny<string>()));
        }

        [Fact]
        public async Task GuestsLandingInLobbyAreGivenMembershipIntoProject()
        {
            await _target.SetProjectGuestContextAsync(_defaultProjectId, _defaultAccessCode, _currentUserId, null);

            _projectAccessApiMock
                .Verify(x => x.GrantProjectMembershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IEnumerable<KeyValuePair<string, string>>>()));
        }

        [Fact]
        public async Task SetProjectGuestContextThrowsInvalidOperationExceptionWhenMembershipCallFails()
        {
            _projectAccessApiMock
                .Setup(x => x.GrantProjectMembershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IEnumerable<KeyValuePair<string, string>>>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.InternalServerError));

            await Assert.ThrowsAsync<InvalidOperationException>(() => _target.SetProjectGuestContextAsync(_defaultProjectId, _defaultAccessCode, _currentUserId, null));
        }

        [Fact]
        public async Task NonGuestsAreGivenAccessIfTheyHaveAccessToProject()
        {
            _projectGuestContextServiceMock
                .Setup(x => x.IsGuestAsync(It.IsAny<string>()))
                .ReturnsAsync(false);

            var response = await _target.SetProjectGuestContextAsync(_defaultProjectId, null, _currentUserId, _userWithTenantTenantId);

            Assert.True(response.UserHasAccess);
        }

        [Fact]
        public async Task NonGuestsAreDeniedAccessIfTheyHaveInsufficientProjectAccess()
        { 
            _projectGuestContextServiceMock
                .Setup(x => x.IsGuestAsync(It.IsAny<string>()))
                .ReturnsAsync(false);

            _projectAccessApiMock
                .Setup(x => x.GetProjectMemberUserIdsAsync(_defaultProjectId, MemberRoleFilter.FullUser, null))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, (new List<Guid>()).AsEnumerable()));

            var response = await _target.SetProjectGuestContextAsync(_defaultProjectId, "code", _currentUserId, Guid.NewGuid());

            Assert.False(response.UserHasAccess);
        }
    }
}