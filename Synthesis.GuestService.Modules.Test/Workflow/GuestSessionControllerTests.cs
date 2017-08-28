using FluentValidation;
using FluentValidation.Results;
using Moq;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.GuestService.Dao.Models;
using Synthesis.GuestService.Validators;
using Synthesis.GuestService.Workflow.Controllers;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Workflow
{
    public class GuestSessionControllerTests
    {
        private readonly GuestSessionController _target;
        private readonly Mock<IRepository<GuestSession>> _guestSessionRepositoryMock;
        private readonly Mock<IEventService> _eventServiceMock = new Mock<IEventService>();
        private readonly Mock<IValidator> _validatorMock = new Mock<IValidator>();
        private readonly Mock<IValidatorLocator> _guestSessionValidator = new Mock<IValidatorLocator>();
        private readonly Mock<ILogger> _loggerMock = new Mock<ILogger>();
        private readonly GuestSession _defaultGuestSession;

        public GuestSessionControllerTests()
        {
            var repositoryFactoryMock = new Mock<IRepositoryFactory>();
            _guestSessionRepositoryMock = new Mock<IRepository<GuestSession>>();
            _defaultGuestSession = new GuestSession() { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid() };

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>()))
                .ReturnsAsync(_defaultGuestSession);

            _guestSessionRepositoryMock
                .Setup(x => x.CreateItemAsync(It.IsAny<GuestSession>()))
                .ReturnsAsync((GuestSession participant) => participant);

            _guestSessionRepositoryMock
                .Setup(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestSession>()))
                .ReturnsAsync((Guid id, GuestSession participant) => participant);

            repositoryFactoryMock
                .Setup(x => x.CreateRepository<GuestSession>())
                .Returns(_guestSessionRepositoryMock.Object);

            _validatorMock
               .Setup(v => v.ValidateAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new ValidationResult());

            _guestSessionValidator
                .Setup(g => g.GetValidator(It.IsAny<Type>()))
                .Returns(_validatorMock.Object);

            _target = new GuestSessionController(repositoryFactoryMock.Object, _guestSessionValidator.Object, _eventServiceMock.Object, _loggerMock.Object);
        }

        #region CREATE Tests
        [Fact]
        public async Task CreateNewGuestSessionBussesEvent()
        {
            await _target.CreateGuestSessionAsync(_defaultGuestSession);
            _eventServiceMock.Verify(x => x.PublishAsync(It.IsAny<ServiceBusEvent<GuestSession>>()));
        }

        [Fact]
        public async Task CreateNewGuestSessionSetsProjectId()
        {
            var result = await _target.CreateGuestSessionAsync(_defaultGuestSession);
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.ProjectId);
            Assert.Equal(_defaultGuestSession.ProjectId, result.ProjectId);
        }

        [Fact]
        public async Task CreateNewGuestSessionSetsProjectAccessCode()
        {
            var result = await _target.CreateGuestSessionAsync(_defaultGuestSession);
            Assert.NotNull(result);
            Assert.NotEqual(string.Empty, result.ProjectAccessCode);
            Assert.Equal(_defaultGuestSession.ProjectAccessCode, result.ProjectAccessCode);
        }

        [Fact]
        public async Task CreateGuestSessionReturnsProvidedGuestSession()
        {
            _defaultGuestSession.Id = Guid.NewGuid();
            _defaultGuestSession.UserId = Guid.NewGuid();
            _defaultGuestSession.ProjectId = Guid.NewGuid();
            _defaultGuestSession.ProjectAccessCode = "123123";

            var result = await _target.CreateGuestSessionAsync(_defaultGuestSession);
            Assert.NotNull(result);
            Assert.Equal(_defaultGuestSession.Id, result.Id);
            Assert.Equal(_defaultGuestSession.UserId, result.UserId);
            Assert.Equal(_defaultGuestSession.ProjectId, result.ProjectId);
            Assert.Equal(_defaultGuestSession.ProjectAccessCode, result.ProjectAccessCode);
        }

        [Fact]
        public async Task CreateGuestSessionVerifyCalled()
        {
            await _target.CreateGuestSessionAsync(_defaultGuestSession);
            _guestSessionRepositoryMock.Verify(x => x.CreateItemAsync(It.IsAny<GuestSession>()));
        }
        #endregion

        #region GET Tests
        [Fact]
        public async Task GetGuestSessionReturnsProjectIfExists()
        {
            var result = await _target.GetGuestSessionAsync(Guid.NewGuid());
            Assert.IsType<GuestSession>(result);
        }

        [Fact]
        public async Task GetGuestSessionVerifyCalled()
        {
            var id = Guid.NewGuid();
            await _target.GetGuestSessionAsync(id);
            _guestSessionRepositoryMock.Verify(x => x.GetItemAsync(It.IsAny<Guid>()));
        }

        [Fact]
        public async Task GetGuestSessionThrowsNotFoundOnDocumentNotFound()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>()))
                .Throws(new NotFoundException("GuestSession could not be found."));

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.GetGuestSessionAsync(It.IsAny<Guid>()));
        }
        #endregion

        #region UPDATE Tests
        [Fact]
        public async Task UpdateOfGuestIniviteBussesEvent()
        {
            await _target.UpdateGuestSessionAsync(_defaultGuestSession.Id, _defaultGuestSession);
            _eventServiceMock.Verify(x => x.PublishAsync(It.IsAny<ServiceBusEvent<GuestSession>>()));
        }

        [Fact]
        public async Task UpdateGuestSessionVerifyCalled()
        {
            await _target.UpdateGuestSessionAsync(_defaultGuestSession.Id, _defaultGuestSession);
            _guestSessionRepositoryMock.Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestSession>()));
        }

        [Fact]
        public async Task UpdateGuestSessionThrowsNotFoundOnDocumentNotFound()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.UpdateItemAsync(It.IsAny<Guid>(), _defaultGuestSession))
                .Throws(new NotFoundException("GuestSession could not be found."));

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.UpdateGuestSessionAsync(_defaultGuestSession.Id, _defaultGuestSession));
        }
        #endregion
    }
}
