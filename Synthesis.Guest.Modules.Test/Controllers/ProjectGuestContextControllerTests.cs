using System;
using System.Threading.Tasks;
using Moq;
using Synthesis.DocumentStorage;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.InternalApi.Enums;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.InternalApi.Services;
using Synthesis.PrincipalService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Api;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Controllers
{
    public class ProjectGuestContextControllerTests
    {
        private readonly Mock<IGuestSessionController> _guestSessionControllerMock = new Mock<IGuestSessionController>();
        private readonly Mock<IProjectLobbyStateController> _projectLobbyStateControllerMock = new Mock<IProjectLobbyStateController>();
        private readonly Mock<IProjectGuestContextService> _projectGuestContextServiceMock = new Mock<IProjectGuestContextService>();
        private readonly Mock<IProjectAccessApi> _projectAccessApiMock = new Mock<IProjectAccessApi>();
        private readonly Mock<IProjectApi> _projectApiMock = new Mock<IProjectApi>();
        private readonly Mock<IUserApi> _userApiMock = new Mock<IUserApi>();
        private readonly ProjectGuestContextController _target;
        private readonly Mock<IRepository<GuestSession>> _guestSessionRepositoryMock = new Mock<IRepository<GuestSession>>();
        private readonly Guid _currentUserId = Guid.NewGuid();
        private readonly ProjectGuestContext _defaultProjectGuestContext = new ProjectGuestContext()
        {
            GuestSessionId = Guid.NewGuid(),
            GuestState = GuestState.InLobby,
            ProjectId = Guid.NewGuid(),
            TenantId = Guid.NewGuid()
        };

        public ProjectGuestContextControllerTests()
        {
            var repositoryFactoryMock = new Mock<IRepositoryFactory>();
            repositoryFactoryMock
                .Setup(x => x.CreateRepository<GuestSession>())
                .Returns(_guestSessionRepositoryMock.Object);

            _projectGuestContextServiceMock
                .Setup(x => x.GetProjectGuestContextAsync())
                .ReturnsAsync(_defaultProjectGuestContext);

            _guestSessionControllerMock
                .Setup(x => x.UpdateGuestSessionStateAsync(It.IsAny<UpdateGuestSessionStateRequest>()))
                .ReturnsAsync(new UpdateGuestSessionStateResponse());

            _target = new ProjectGuestContextController(repositoryFactoryMock.Object,
                _guestSessionControllerMock.Object,
                _projectLobbyStateControllerMock.Object,
                _projectGuestContextServiceMock.Object,
                _projectAccessApiMock.Object,
                _projectApiMock.Object,
                _userApiMock.Object);
        }

        [Fact]
        public async Task GuestSessionIsEndedIfProjectIdIsEmpty()
        {
            await _target.SetProjectGuestContextAsync(Guid.Empty, null, _currentUserId);

            _guestSessionControllerMock
                .Verify(x => x.UpdateGuestSessionStateAsync(It.Is<UpdateGuestSessionStateRequest>(y =>
                    y.GuestSessionId == _defaultProjectGuestContext.GuestSessionId &&
                    y.GuestSessionState == GuestState.Ended)));
        }

        [Fact]
        public async Task ProjectGuestContextIsResetIfProjectIdIsEmpty()
        {
            await _target.SetProjectGuestContextAsync(Guid.Empty, null, _currentUserId);

            _projectGuestContextServiceMock
                .Verify(y => y.SetProjectGuestContextAsync(It.Is<ProjectGuestContext>(x =>
                    x.ProjectId == Guid.Empty &&
                    x.GuestSessionId == Guid.Empty)));
        }

        [Fact]
        public async Task InvalidOperationIsThrownIfGuestSessionCannotBeCleared()
        {
            _guestSessionControllerMock
                .Setup(x => x.UpdateGuestSessionStateAsync(It.IsAny<UpdateGuestSessionStateRequest>()))
                .ReturnsAsync(new UpdateGuestSessionStateResponse() { ResultCode = UpdateGuestSessionStateResultCodes.Failed });

            await Assert.ThrowsAsync<InvalidOperationException>(() => _target.SetProjectGuestContextAsync(Guid.Empty, null, _currentUserId));
        }
    }
}