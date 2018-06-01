using FluentValidation;
using FluentValidation.Results;
using Moq;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.GuestService.Controllers;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using System;
using System.Net; 
using System.Threading;
using System.Threading.Tasks;
using Synthesis.GuestService.Email;
using Synthesis.GuestService.Exceptions;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.Http.Microservice;
using Synthesis.Http.Microservice.Models;
using Synthesis.PrincipalService.InternalApi.Api;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.ProjectService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Models;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Controllers
{
    public class GuestInviteControllerTests
    {
        public GuestInviteControllerTests()
        {
            var repositoryFactoryMock = new Mock<IRepositoryFactory>();
            _guestInviteRepositoryMock = new Mock<IRepository<GuestInvite>>();
            _defaultGuestInvite = new GuestInvite { Id = Guid.NewGuid(), InvitedBy = Guid.NewGuid(), ProjectId = Guid.NewGuid(), CreatedDateTime = DateTime.UtcNow };

            _userApiMock.Setup(x => x.GetUserAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, User.GuestUserExample()));

            _projectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, Project.Example()));

            _projectApiMock.Setup(x => x.ResetGuestAccessCodeAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create<string>(HttpStatusCode.OK, Project.Example().GuestAccessCode));

            _emailServiceMock.Setup(x => x.SendGuestInviteEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK));

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

            _target = new GuestInviteController(_userApiMock.Object, _projectApiMock.Object, _emailServiceMock.Object, repositoryFactoryMock.Object, _validatorLocator.Object, _eventServiceMock.Object, loggerFactoryMock.Object);
        }

        private readonly GuestInviteController _target;
        private readonly Mock<IUserApi> _userApiMock = new Mock<IUserApi>();
        private readonly Mock<IProjectApi> _projectApiMock = new Mock<IProjectApi>();
        private readonly Mock<IEmailSendingService> _emailServiceMock = new Mock<IEmailSendingService>();
        private readonly Mock<IRepository<GuestInvite>> _guestInviteRepositoryMock;
        private readonly Mock<IEventService> _eventServiceMock = new Mock<IEventService>();
        private readonly Mock<IValidator> _validatorMock = new Mock<IValidator>();
        private readonly Mock<IValidatorLocator> _validatorLocator = new Mock<IValidatorLocator>();
        private readonly GuestInvite _defaultGuestInvite;

        [Fact]
        public async Task CreateGuestInvitCallsCreate()
        {
            await _target.CreateGuestInviteAsync(_defaultGuestInvite);
            _guestInviteRepositoryMock.Verify(x => x.CreateItemAsync(It.IsAny<GuestInvite>()));
        }

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
        public async Task CreateNewGuestInviteBussesEvent()
        {
            await _target.CreateGuestInviteAsync(_defaultGuestInvite);
            _eventServiceMock.Verify(x => x.PublishAsync(It.IsAny<ServiceBusEvent<GuestInvite>>()));
        }

        [Fact]
        public async Task CreateNewGuestInviteGetsProject()
        {
            await _target.CreateGuestInviteAsync(_defaultGuestInvite);
            _projectApiMock.Verify(x => x.GetProjectByIdAsync(It.IsAny<Guid>()));
        }

        [Fact]
        public async Task CreateNewGuestInviteThrowsGetProjectExceptionWhenGetProjectFails()
        {
            _projectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create<Project>(HttpStatusCode.NotFound, new ErrorResponse()));

            await Assert.ThrowsAsync<GetProjectException>(async () => await _target.CreateGuestInviteAsync(_defaultGuestInvite));
        }

        [Fact]
        public async Task CreateNewGuestInviteThrowsGetUserExceptionWhenGetUserFails()
        {
            _userApiMock.Setup(x => x.GetUserAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create<User>(HttpStatusCode.NotFound, new ErrorResponse()));

            await Assert.ThrowsAsync<GetUserException>(async () => await _target.CreateGuestInviteAsync(_defaultGuestInvite));
        }

        [Fact]
        public async Task CreateNewGuestInviteThrowsResetAccessCodeExceptionWhenResetAccessCodeFails()
        {
            var project = Project.Example();
            project.GuestAccessCode = null;
            _projectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, project));

            _projectApiMock.Setup(x => x.ResetGuestAccessCodeAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create<string>(HttpStatusCode.NotFound, new ErrorResponse()));

            await Assert.ThrowsAsync<ResetAccessCodeException>(async () => await _target.CreateGuestInviteAsync(_defaultGuestInvite));
        }

        [Fact]
        public async Task CreateNewGuestInviteGetsUser()
        {
            await _target.CreateGuestInviteAsync(_defaultGuestInvite);
            _userApiMock.Verify(x => x.GetUserAsync(It.Is<Guid>(id => id == _defaultGuestInvite.InvitedBy)));
        }

        [Fact]
        public async Task CreateNewGuestInviteResetsAccessCodeIfCodeIsMissing()
        {
            var project = Project.Example();
            project.GuestAccessCode = null;
            _projectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, project));

            await _target.CreateGuestInviteAsync(_defaultGuestInvite);
            _projectApiMock.Verify(x => x.ResetGuestAccessCodeAsync(It.IsAny<Guid>()));
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
        public async Task GetGuestInviteCallsGet()
        {
            var id = Guid.NewGuid();
            await _target.GetGuestInviteAsync(id);
            _guestInviteRepositoryMock.Verify(x => x.GetItemAsync(It.IsAny<Guid>()));
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