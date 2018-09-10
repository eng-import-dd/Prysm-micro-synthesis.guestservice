﻿using System;
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
using Synthesis.PrincipalService.InternalApi.Api;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.ProjectService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Enumerations;
using Synthesis.ProjectService.InternalApi.Models;
using Synthesis.ProjectService.InternalApi.Constants;
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
        private readonly List<KeyValuePair<string, string>> _defaultImpersonateProjectTenantHeaders;
        private readonly ProjectGuestContext _defaultProjectGuestContext;
        private readonly ProjectLobbyState _defaultProjectLobbyState;
        private readonly User _defaultUser;
        private readonly string _defaultAccessCode = Guid.NewGuid().ToString();
        private readonly GuestSession _defaultGuestSession;

        public ProjectGuestContextControllerTests()
        {
            _defaultProject = new Project() { Id = _defaultProjectId, TenantId = _projectTenantId, GuestAccessCode = _defaultAccessCode };
            _defaultImpersonateProjectTenantHeaders = new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>(HeaderKeys.ImpersonateTenant, _defaultProject.TenantId.ToString()) };
            _defaultUser = new User { Id = _currentUserId, Username = "George C" };
            _defaultProjectLobbyState = new ProjectLobbyState() { LobbyState = LobbyState.Normal, ProjectId = _defaultProjectId };
            _defaultGuestSession = new GuestSession { Id = Guid.NewGuid(), ProjectId = _defaultProjectId, UserId = _currentUserId, ProjectAccessCode = _defaultAccessCode, GuestSessionState = GuestState.InLobby };
            _defaultProjectGuestContext = new ProjectGuestContext()
            {
                GuestSessionId = _defaultGuestSession.Id,
                GuestState = Synthesis.Guest.ProjectContext.Enums.GuestState.InLobby,
                ProjectId =_defaultProjectId,
                TenantId = _projectTenantId
            };

            var repositoryFactoryMock = new Mock<IRepositoryFactory>();
            repositoryFactoryMock
                .Setup(x => x.CreateRepository<GuestSession>())
                .Returns(_guestSessionRepositoryMock.Object);

            _projectGuestContextServiceMock
                .Setup(x => x.GetProjectGuestContextAsync())
                .ReturnsAsync(_defaultProjectGuestContext);

            _projectAccessApiMock
                .Setup(x => x.GrantProjectMembershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), null))
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
                .Setup(x => x.GetProjectMemberUserIdsAsync(_defaultProjectId, MemberRoleFilter.FullUser, _defaultImpersonateProjectTenantHeaders))
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
                    x.GuestSessionId == Guid.Empty)));
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

        [Theory]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.Forbidden)]
        [InlineData(HttpStatusCode.BadGateway)]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.Unauthorized)]
        public async Task SetProjectGuestContext_IfProjectCannotBeFetchedByServiceToServiceClientForGuest_ThrowsInvalidOperationException(HttpStatusCode statusCode)
        {
            _projectGuestContextServiceMock
                .Setup(x => x.IsGuestAsync())
                .ReturnsAsync(true);

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
                .Setup(x => x.GetProjectMemberUserIdsAsync(_defaultProjectId, MemberRoleFilter.FullUser, _defaultImpersonateProjectTenantHeaders))
                .ReturnsAsync(MicroserviceResponse.Create(statusCode, default(IEnumerable<Guid>)));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _target.SetProjectGuestContextAsync(_defaultProjectId, null, _currentUserId, _noTenantId));
        }


        [Fact]
        public async Task SetProjectGuestContext_CallsGetProjectMemberUserids()
        {
            await _target.SetProjectGuestContextAsync(_defaultProjectId, _defaultAccessCode, _currentUserId, _noTenantId);

            _projectAccessApiMock
                .Verify(y => y.GetProjectMemberUserIdsAsync(_defaultProjectId, MemberRoleFilter.FullUser, _defaultImpersonateProjectTenantHeaders));
        }

        [Fact]
        public async Task SetProjectGuestContext_CallsGetProjectGuestContext()
        {
            await _target.SetProjectGuestContextAsync(_defaultProjectId, _defaultAccessCode, _currentUserId, _noTenantId);

            _projectGuestContextServiceMock
                .Verify(y => y.GetProjectGuestContextAsync());
        }

        [Fact]
        public async Task SetProjectGuestContext_IfUserIsAGuestInTheSameAccountAndHasBeenPromoted_ClearsProjectGuestContext()
        {
            _projectGuestContextServiceMock
                .Setup(x => x.GetProjectGuestContextAsync())
                .ReturnsAsync(new ProjectGuestContext()
                {
                    GuestSessionId = Guid.NewGuid(),
                    GuestState = Guest.ProjectContext.Enums.GuestState.PromotedToProjectMember,
                    ProjectId = _defaultProjectId,
                    TenantId = _projectTenantId
                });

            await _target.SetProjectGuestContextAsync(_defaultProjectId, null, _currentUserId, _projectTenantId);

            _projectGuestContextServiceMock
                .Verify(y => y.SetProjectGuestContextAsync(It.Is<ProjectGuestContext>(x =>
                    x.ProjectId == Guid.Empty &&
                    x.GuestSessionId == Guid.Empty)));
        }

        [Fact]
        public async Task SetProjectGuestContext_WhenUserIsAGuestInLobby_CallsGuestSessionRepositoryGetItems()
        {
            _projectGuestContextServiceMock
            .Setup(x => x.GetProjectGuestContextAsync())
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

            _guestSessionRepositoryMock.Verify(y => y.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task SetProjectGuestContext_WhenUserFromSameTenantIsNotProjectMemberAndEntersLobby_UserHasAccessIsFalse()
        {
            _projectGuestContextServiceMock
                .Setup(x => x.GetProjectGuestContextAsync())
                .ReturnsAsync(default(ProjectGuestContext));

            _projectAccessApiMock
                .Setup(x => x.GetProjectMemberUserIdsAsync(_defaultProjectId, MemberRoleFilter.FullUser, _defaultImpersonateProjectTenantHeaders))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, (new List<Guid>()).AsEnumerable()));

            var response = await _target.SetProjectGuestContextAsync(_defaultProjectId, "code", _currentUserId, Guid.NewGuid());

            Assert.False(response.UserHasAccess);
        }

        [Fact]
        public async Task SetProjectGuestContext_WhenUserFromSameTenantIsProjectMemberAndEntersLobby_UserHasAccessIsTrue()
        {
            _projectGuestContextServiceMock
                .Setup(x => x.GetProjectGuestContextAsync())
                .ReturnsAsync(default(ProjectGuestContext));

            var response = await _target.SetProjectGuestContextAsync(_defaultProjectId, null, _currentUserId, _projectTenantId);

            Assert.True(response.UserHasAccess);
        }

        //[Fact]
        //public async Task SetProjectGuestContext_WhenUserFromSameTenantIsProjectMemberIsAdmittedFromTheLobby_UserHasAccessIsTrue()
        //{
        //    _projectGuestContextServiceMock
        //        .Setup(x => x.GetProjectGuestContextAsync())
        //        .ReturnsAsync(new ProjectGuestContext()
        //        {
        //            GuestSessionId = Guid.NewGuid(),
        //            GuestState = Guest.ProjectContext.Enums.GuestState.PromotedToProjectMember,
        //            ProjectId = _defaultProjectId,
        //            TenantId = _projectTenantId
        //        });

        //    _guestSessionRepositoryMock
        //        .Setup()

        //    var response = await _target.SetProjectGuestContextAsync(_defaultProjectId, null, _currentUserId, _projectTenantId);

        //    Assert.True(response.UserHasAccess);
        //}


        [Fact]
        public async Task SetProjectGuestContext_WhenGuestsReachLobbyAndAreGrantedGuestSession_AreGivenMembershipIntoProject()
        {
            _projectGuestContextServiceMock
                .SetupSequence(x => x.GetProjectGuestContextAsync())
                .Returns(Task.FromResult(default(ProjectGuestContext)))
                .Returns(Task.FromResult(_defaultProjectGuestContext));

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemAsync(_defaultGuestSession.Id, It.IsAny<QueryOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_defaultGuestSession);
            
            await _target.SetProjectGuestContextAsync(_defaultProjectId, _defaultAccessCode, _currentUserId, null);

            _projectAccessApiMock
                .Verify(x => x.GrantProjectMembershipAsync(_currentUserId, _defaultProjectId, null));
        }

        [Fact]
        public async Task SetProjectGuestContext_WhenGrantMembershipCallFails_ThrowsInvalidOperationException()
        {
            _projectGuestContextServiceMock
                .Setup(x => x.GetProjectGuestContextAsync())
                .ReturnsAsync(default(ProjectGuestContext));

            _projectAccessApiMock
                .Setup(x => x.GrantProjectMembershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), null))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.InternalServerError));

            await Assert.ThrowsAsync<InvalidOperationException>(() => _target.SetProjectGuestContextAsync(_defaultProjectId, _defaultAccessCode, _currentUserId, null));
        }


    }
}