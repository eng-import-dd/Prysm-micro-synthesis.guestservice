using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Synthesis.DocumentStorage;
using Synthesis.DocumentStorage.TestTools.Mocks;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.InternalApi.Models;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Controllers
{
    public class GuestTenantControllerTests
    {
        private readonly GuestTenantController _target;
        private readonly Mock<IRepository<GuestInvite>> _guestInviteRepositoryMock = new Mock<IRepository<GuestInvite>>();
        private readonly Mock<IRepository<GuestSession>> _guestSessionRepositoryMock = new Mock<IRepository<GuestSession>>();
        private readonly List<GuestSession> _guestSessions = new List<GuestSession>();
        private readonly List<GuestInvite> _guestInvites = new List<GuestInvite>();

        public GuestTenantControllerTests()
        {
            var repositoryFactoryMock = new Mock<IRepositoryFactory>();

            _guestSessionRepositoryMock
                .SetupCreateItemQuery(o => _guestSessions);

            _guestInviteRepositoryMock
                .SetupCreateItemQuery(o => _guestInvites);

            repositoryFactoryMock
                .Setup(x => x.CreateRepositoryAsync<GuestInvite>(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_guestInviteRepositoryMock.Object);

            repositoryFactoryMock
                .Setup(x => x.CreateRepositoryAsync<GuestSession>(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_guestSessionRepositoryMock.Object);

            _target = new GuestTenantController(repositoryFactoryMock.Object);
        }

        [Fact]
        public async Task GetTenantIdsGetsTenantsFromGuestSessions()
        {
            var userId = Guid.NewGuid();

            await _target.GetTenantIdsForUserAsync(userId);

            _guestSessionRepositoryMock
                .Verify(x => x.CreateItemQuery(It.IsAny<BatchOptions>()));
        }

        [Fact]
        public async Task GetTenantIdsGetsTenantsFromGuestInvites()
        {
            var userId = Guid.NewGuid();

            await _target.GetTenantIdsForUserAsync(userId);

            _guestInviteRepositoryMock
                .Verify(x => x.CreateItemQuery(It.IsAny<BatchOptions>()));
        }

        [Fact]
        public async Task GetTenantIdsExcludesDuplicateIds()
        {
            var userId = Guid.NewGuid();
            var tenantIdOne = Guid.NewGuid();
            var tenantIdTwo = Guid.NewGuid();
            var tenantIdThree = Guid.NewGuid();

            _guestSessions.Add(new GuestSession { UserId = userId, ProjectTenantId = tenantIdOne});
            _guestSessions.Add(new GuestSession { UserId = userId, ProjectTenantId = tenantIdTwo});
            _guestInvites.Add(new GuestInvite { UserId = userId,  ProjectTenantId = tenantIdTwo});
            _guestInvites.Add(new GuestInvite { UserId = userId,  ProjectTenantId = tenantIdThree});

            var tenantIds = await _target.GetTenantIdsForUserAsync(userId);

            Assert.Collection(tenantIds,
                tenantId => Assert.Equal(tenantId, tenantIdOne),
                tenantId => Assert.Equal(tenantId, tenantIdTwo),
                tenantId => Assert.Equal(tenantId, tenantIdThree));
        }
    }
}