using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.GuestService.Dao.Models;
using Synthesis.GuestService.Workflow.Controllers;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Workflow
{
    public class GuestInviteControllerTests
    {
        public GuestInviteControllerTests()
        {
            var repositoryFactoryMock = new Mock<IRepositoryFactory>();
            _guestInviteRepositoryMock = new Mock<IRepository<GuestInvite>>();
            _defaultGuestInvite = new GuestInvite { Id = Guid.NewGuid(), InvitedBy = Guid.NewGuid(), ProjectId = Guid.NewGuid(), CreatedDateTime = DateTime.UtcNow };

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

            _validatorMock
                .Setup(v => v.ValidateAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());

            _guestInviteValidator
                .Setup(g => g.GetValidator(It.IsAny<Type>()))
                .Returns(_validatorMock.Object);

            _target = new GuestInviteController(repositoryFactoryMock.Object, _guestInviteValidator.Object, _eventServiceMock.Object, _loggerMock.Object);
        }

        private readonly GuestInviteController _target;
        private readonly Mock<IRepository<GuestInvite>> _guestInviteRepositoryMock;
        private readonly Mock<IEventService> _eventServiceMock = new Mock<IEventService>();
        private readonly Mock<IValidator> _validatorMock = new Mock<IValidator>();
        private readonly Mock<IValidatorLocator> _guestInviteValidator = new Mock<IValidatorLocator>();
        private readonly Mock<ILogger> _loggerMock = new Mock<ILogger>();
        private readonly GuestInvite _defaultGuestInvite;

        [Fact]
        public async Task CreateGuestInviteReturnsProvidedGuestInvite()
        {
            _defaultGuestInvite.Id = Guid.NewGuid();
            _defaultGuestInvite.InvitedBy = Guid.NewGuid();
            _defaultGuestInvite.ProjectId = Guid.NewGuid();

            var result = await _target.CreateGuestInviteAsync(_defaultGuestInvite);
            Assert.NotNull(result);
            Assert.Equal(_defaultGuestInvite.Id, result.Id);
            Assert.Equal(_defaultGuestInvite.InvitedBy, result.InvitedBy);
            Assert.Equal(_defaultGuestInvite.ProjectId, result.ProjectId);
        }

        [Fact]
        public async Task CreateGuestInviteVerifyCalled()
        {
            await _target.CreateGuestInviteAsync(_defaultGuestInvite);
            _guestInviteRepositoryMock.Verify(x => x.CreateItemAsync(It.IsAny<GuestInvite>()));
        }

        [Fact]
        public async Task CreateNewGuestInviteBussesEvent()
        {
            await _target.CreateGuestInviteAsync(_defaultGuestInvite);
            _eventServiceMock.Verify(x => x.PublishAsync(It.IsAny<ServiceBusEvent<GuestInvite>>()));
        }

        [Fact]
        public async Task CreateNewGuestInviteSetsInvitedBy()
        {
            var result = await _target.CreateGuestInviteAsync(_defaultGuestInvite);
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.InvitedBy);
            Assert.Equal(_defaultGuestInvite.InvitedBy, result.InvitedBy);
        }

        [Fact]
        public async Task CreateNewGuestInviteSetsProjectId()
        {
            var result = await _target.CreateGuestInviteAsync(_defaultGuestInvite);
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.ProjectId);
            Assert.Equal(_defaultGuestInvite.ProjectId, result.ProjectId);
        }

        [Fact]
        public async Task GetGuestInviteReturnsProjectIfExists()
        {
            var result = await _target.GetGuestInviteAsync(Guid.NewGuid());
            Assert.IsType<GuestInvite>(result);
        }

        [Fact]
        public async Task GetGuestInviteThrowsNotFoundOnDocumentNotFound()
        {
            _guestInviteRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>()))
                .Throws(new NotFoundException("GuestInvite not found"));

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.GetGuestInviteAsync(It.IsAny<Guid>()));
        }

        [Fact]
        public async Task GetGuestInviteVerifyCalled()
        {
            var id = Guid.NewGuid();
            await _target.GetGuestInviteAsync(id);
            _guestInviteRepositoryMock.Verify(x => x.GetItemAsync(It.IsAny<Guid>()));
        }

        [Fact]
        public async Task UpdateGuestInviteThrowsNotFoundOnNotFoundException()
        {
            _guestInviteRepositoryMock
                .Setup(x => x.UpdateItemAsync(It.IsAny<Guid>(), _defaultGuestInvite))
                .Throws(new NotFoundException("Message"));

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.UpdateGuestInviteAsync(_defaultGuestInvite));
        }

        [Fact]
        public async Task UpdateGuestInviteVerifyCalled()
        {
            await _target.UpdateGuestInviteAsync(_defaultGuestInvite);
            _guestInviteRepositoryMock.Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestInvite>()));
        }

        [Fact]
        public async Task UpdateOfGuestIniviteBussesEvent()
        {
            await _target.UpdateGuestInviteAsync(_defaultGuestInvite);
            _eventServiceMock.Verify(x => x.PublishAsync(It.IsAny<ServiceBusEvent<GuestInvite>>()));
        }
    }
}