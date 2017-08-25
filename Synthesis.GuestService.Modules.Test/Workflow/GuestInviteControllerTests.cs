using System;
using System.Threading.Tasks;
using Moq;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.Logging;
using Synthesis.GuestService.Dao.Models;
using Synthesis.GuestService.Workflow.Controllers;
using Synthesis.GuestService.Validators;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Workflow
{
    public class GuestInviteControllerTests
    {
        private readonly GuestInviteController _target;
        private readonly Mock<IRepository<GuestInvite>> _guestInviteRepositoryMock;
        private readonly Mock<IEventService> _eventServiceMock = new Mock<IEventService>();
        private readonly Mock<IValidatorLocator> _guestInviteValidator = new Mock<IValidatorLocator>();
        private readonly Mock<ILogger> _loggerMock = new Mock<ILogger>();
        private readonly GuestInvite _defaultGuestInvite;

        public GuestInviteControllerTests()
        {
            var repositoryFactoryMock = new Mock<IRepositoryFactory>();
            _guestInviteRepositoryMock = new Mock<IRepository<GuestInvite>>();
            _defaultGuestInvite = new GuestInvite() { Id = Guid.NewGuid(), InvitedBy = Guid.NewGuid(), ProjectId = Guid.NewGuid(), CreatedDateTime = DateTime.UtcNow };

            _guestInviteRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(_defaultGuestInvite);

            _guestInviteRepositoryMock
                .Setup(x => x.CreateItemAsync(It.IsAny<GuestInvite>()))
                .ReturnsAsync((GuestInvite guestInvite) => guestInvite);

            _guestInviteRepositoryMock
                .Setup(x => x.UpdateItemAsync(_defaultGuestInvite.Id, It.IsAny<GuestInvite>()))
                .ReturnsAsync((Guid id, GuestInvite guestInvite) => guestInvite);

            repositoryFactoryMock
                .Setup(x => x.CreateRepository<GuestInvite>())
                .Returns(_guestInviteRepositoryMock.Object);

            _target = new GuestInviteController(repositoryFactoryMock.Object, _guestInviteValidator.Object, _eventServiceMock.Object, _loggerMock.Object);
        }

        //// -- CREATE tests
        //[Fact]
        //public async Task CreateNewGuestInviteBussesEvent()
        //{
        //    await _target.CreateGuestInviteAsync(_defaultGuestInvite);
        //    _eventServiceMock.Verify(x => x.PublishAsync(It.IsAny<ServiceBusEvent<GuestInvite>>()));
        //}
        //[Fact]
        //public async Task CreateNewGuestInviteSetsProjectId()
        //{
        //    var result = await _target.CreateGuestInviteAsync(_defaultGuestInvite);
        //    Assert.NotNull(result);
        //    Assert.NotEqual(Guid.Empty, result.ProjectId);
        //    Assert.Equal(_defaultGuestInvite.ProjectId, result.ProjectId);
        //}
        //[Fact]
        //public async Task CreateNewGuestInviteSetsInvitedBy()
        //{
        //    var result = await _target.CreateGuestInviteAsync(_defaultGuestInvite);
        //    Assert.NotNull(result);
        //    Assert.NotEqual(Guid.Empty, result.InvitedBy);
        //    Assert.Equal(_defaultGuestInvite.InvitedBy, result.InvitedBy);
        //}
        //[Fact]
        //public async Task CreateGuestInviteReturnsProvidedGuestInvite()
        //{
        //    _defaultGuestInvite.Id = Guid.NewGuid();
        //    _defaultGuestInvite.InvitedBy = Guid.NewGuid();
        //    _defaultGuestInvite.ProjectId = Guid.NewGuid();

        //    var result = await _target.CreateGuestInviteAsync(_defaultGuestInvite);
        //    Assert.NotNull(result);
        //    Assert.Equal(_defaultGuestInvite.Id, result.Id);
        //    Assert.Equal(_defaultGuestInvite.InvitedBy, result.InvitedBy);
        //    Assert.Equal(_defaultGuestInvite.ProjectId, result.ProjectId);
        //}
        //[Fact]
        //public async Task CreateGuestInviteVerifyCalled()
        //{
        //    await _target.CreateGuestInviteAsync(_defaultGuestInvite);
        //    _guestInviteRepositoryMock.Verify(x => x.CreateItemAsync(It.IsAny<GuestInvite>()));
        //}

        //// -- GET tests
        //[Fact]
        //public async Task GetGuestInviteReturnsProjectIfExists()
        //{
        //    var result = await _target.GetGuestInviteAsync(Guid.NewGuid());
        //    Assert.IsType<GuestInvite>(result);
        //}
        //[Fact]
        //public async Task GetGuestInviteReturnsNullIfParticipantDoesNotExist()
        //{
        //    _guestInviteRepositoryMock
        //        .Setup(x => x.GetItemAsync(It.IsAny<Guid>()))
        //        .ReturnsAsync(() => null);

        //    var guestInviteId = Guid.NewGuid();
        //    var result = await _target.GetGuestInviteAsync(guestInviteId);
        //    Assert.Null(result);
        //}
        //[Fact]
        //public async Task GetGuestInviteVerifyCalled()
        //{
        //    var id = Guid.NewGuid();
        //    await _target.GetGuestInviteAsync(id);
        //    _guestInviteRepositoryMock.Verify(x => x.GetItemAsync(It.IsAny<Guid>()));
        //}
        //[Fact]
        //public async Task GetGuestInviteThrowsNotFoundOnDocumentNotFound()
        //{
        //    _guestInviteRepositoryMock
        //        .Setup(x => x.GetItemAsync(It.IsAny<Guid>()))
        //        .Throws<DocumentNotFoundException>();

        //    await Assert.ThrowsAsync<NotFoundException>(async () => await _target.GetGuestInviteAsync(It.IsAny<Guid>()));
        //}

        //// -- UPDATE tests
        //[Fact]
        //public async Task UpdateOfGuestIniviteBussesEvent()
        //{
        //    await _target.UpdateGuestInviteAsync(_defaultGuestInvite.Id, _defaultGuestInvite);
        //    _eventServiceMock.Verify(x => x.PublishAsync(It.IsAny<ServiceBusEvent<GuestInvite>>()));
        //}
        //[Fact]
        //public async Task UpdateGuestInviteVerifyCalled()
        //{
        //    await _target.UpdateGuestInviteAsync(_defaultGuestInvite.Id, _defaultGuestInvite);
        //    _guestInviteRepositoryMock.Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestInvite>()));
        //}
        //[Fact]
        //public async Task UpdateGuestInviteThrowsNotFoundOnDocumentNotFound()
        //{
        //    _guestInviteRepositoryMock
        //        .Setup(x => x.UpdateItemAsync(It.IsAny<Guid>(), _defaultGuestInvite))
        //        .Throws<DocumentNotFoundException>();

        //    await Assert.ThrowsAsync<NotFoundException>(async () => await _target.UpdateGuestInviteAsync(_defaultGuestInvite.Id, _defaultGuestInvite));
        //}
    }
}
