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
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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
using Synthesis.Serialization;
using Xunit;
using static Synthesis.GuestService.Modules.Test.Utilities.LoopUtilities;

namespace Synthesis.GuestService.Modules.Test.Controllers
{
    public class GuestInviteControllerTests
    {
        public GuestInviteControllerTests()
        {
            var repositoryFactoryMock = new Mock<IRepositoryFactory>();
            _guestInviteRepositoryMock = new Mock<IRepository<GuestInvite>>();
            _defaultGuestInvite = new GuestInvite
            {
                Id = Guid.NewGuid(),
                InvitedBy = Guid.NewGuid(),
                ProjectId = Guid.NewGuid(),
                CreatedDateTime = DateTime.UtcNow
            };

            _defaultProject = new Project
            {
                Id = Guid.NewGuid(),
                GuestAccessCode = Guid.NewGuid().ToString()
            };

            _userApiMock.Setup(x => x.GetUserAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, User.GuestUserExample()));

            _projectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), null))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, Project.Example()));

            _projectApiMock.Setup(x => x.ResetGuestAccessCodeAsync(It.IsAny<Guid>(), null))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, Project.Example()));

            _emailServiceMock.Setup(x => x.SendGuestInviteEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK));

            _guestInviteRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_defaultGuestInvite);

            _guestInviteRepositoryMock
                .Setup(x => x.CreateItemAsync(It.IsAny<GuestInvite>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((GuestInvite guestInvite, CancellationToken c) => guestInvite);

            _guestInviteRepositoryMock
                .Setup(x => x.UpdateItemAsync(_defaultGuestInvite.Id, It.IsAny<GuestInvite>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, GuestInvite guestInvite, UpdateOptions o, CancellationToken c) => guestInvite);

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

            _target = new GuestInviteController(_userApiMock.Object, _projectApiMock.Object, _emailServiceMock.Object, repositoryFactoryMock.Object, _validatorLocator.Object, _eventServiceMock.Object, loggerFactoryMock.Object, _serializerMock.Object);
        }

        private readonly GuestInviteController _target;
        private readonly Mock<IUserApi> _userApiMock = new Mock<IUserApi>();
        private readonly Mock<IProjectApi> _projectApiMock = new Mock<IProjectApi>();
        private readonly Mock<IEmailSendingService> _emailServiceMock = new Mock<IEmailSendingService>();
        private readonly Mock<IRepository<GuestInvite>> _guestInviteRepositoryMock;
        private readonly Mock<IEventService> _eventServiceMock = new Mock<IEventService>();
        private readonly Mock<IValidator> _validatorMock = new Mock<IValidator>();
        private readonly Mock<IValidatorLocator> _validatorLocator = new Mock<IValidatorLocator>();
        private readonly Mock<IObjectSerializer> _serializerMock = new Mock<IObjectSerializer>();
        private readonly GuestInvite _defaultGuestInvite;
        private readonly Project _defaultProject;

        [Fact]
        public async Task CreateGuestInviteCallsCreate()
        {
            await _target.CreateGuestInviteAsync(_defaultGuestInvite, It.IsAny<Guid>());
            _guestInviteRepositoryMock.Verify(x => x.CreateItemAsync(It.IsAny<GuestInvite>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task CreateGuestInviteCallsDeleteItemsToClearOldGuestInvitesForEmailAndProject()
        {
            await _target.CreateGuestInviteAsync(_defaultGuestInvite, It.IsAny<Guid>());
            _guestInviteRepositoryMock.Verify(x => x.DeleteItemsAsync(It.IsAny<Expression<Func<GuestInvite, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task CreateGuestInviteReturnsProvidedGuestInvite()
        {
            _defaultGuestInvite.Id = Guid.NewGuid();
            _defaultGuestInvite.InvitedBy = Guid.NewGuid();
            _defaultGuestInvite.ProjectId = Guid.NewGuid();

            var result = await _target.CreateGuestInviteAsync(_defaultGuestInvite, It.IsAny<Guid>());
            Assert.NotNull(result);
            Assert.Equal(_defaultGuestInvite.Id, result.Id);
            Assert.Equal(_defaultGuestInvite.InvitedBy, result.InvitedBy);
            Assert.Equal(_defaultGuestInvite.ProjectId, result.ProjectId);
        }

        [Fact]
        public async Task CreateNewGuestInviteBussesEvent()
        {
            await _target.CreateGuestInviteAsync(_defaultGuestInvite, It.IsAny<Guid>());
            _eventServiceMock.Verify(x => x.PublishAsync(It.IsAny<ServiceBusEvent<GuestInvite>>()));
        }

        [Fact]
        public async Task CreateNewGuestInviteGetsProject()
        {
            await _target.CreateGuestInviteAsync(_defaultGuestInvite, It.IsAny<Guid>());
            _projectApiMock.Verify(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), null));
        }

        [Fact]
        public async Task CreateNewGuestInviteThrowsGetProjectExceptionWhenGetProjectFails()
        {
            _projectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), null))
                .ReturnsAsync(MicroserviceResponse.Create<Project>(HttpStatusCode.NotFound, new ErrorResponse()));

            await Assert.ThrowsAsync<GetProjectException>(async () => await _target.CreateGuestInviteAsync(_defaultGuestInvite, It.IsAny<Guid>()));
        }

        [Fact]
        public async Task CreateNewGuestInviteThrowsGetUserExceptionWhenGetUserFails()
        {
            _userApiMock.Setup(x => x.GetUserAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create<User>(HttpStatusCode.NotFound, new ErrorResponse()));

            await Assert.ThrowsAsync<GetUserException>(async () => await _target.CreateGuestInviteAsync(_defaultGuestInvite, It.IsAny<Guid>()));
        }

        [Fact]
        public async Task CreateNewGuestInviteThrowsResetAccessCodeExceptionWhenResetAccessCodeFails()
        {
            var project = Project.Example();
            project.GuestAccessCode = null;
            _projectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), null))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, project));

            _projectApiMock.Setup(x => x.ResetGuestAccessCodeAsync(It.IsAny<Guid>(), null))
                .ReturnsAsync(MicroserviceResponse.Create<Project>(HttpStatusCode.NotFound, new ErrorResponse()));

            await Assert.ThrowsAsync<ResetAccessCodeException>(async () => await _target.CreateGuestInviteAsync(_defaultGuestInvite, It.IsAny<Guid>()));
        }

        [Fact]
        public async Task CreateNewGuestInviteGetsUser()
        {
            await _target.CreateGuestInviteAsync(_defaultGuestInvite, It.IsAny<Guid>());
            _userApiMock.Verify(x => x.GetUserAsync(It.Is<Guid>(id => id == _defaultGuestInvite.InvitedBy)));
        }

        [Fact]
        public async Task CreateNewGuestInviteResetsAccessCodeIfCodeIsMissing()
        {
            var project = Project.Example();
            project.GuestAccessCode = null;
            _projectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), null))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, project));

            await _target.CreateGuestInviteAsync(_defaultGuestInvite, It.IsAny<Guid>());
            _projectApiMock.Verify(x => x.ResetGuestAccessCodeAsync(It.IsAny<Guid>(), null));
        }

        [Fact]
        public async Task CreateNewGuestInviteSetsInvitedBy()
        {
            var result = await _target.CreateGuestInviteAsync(_defaultGuestInvite, It.IsAny<Guid>());
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.InvitedBy);
            Assert.Equal(_defaultGuestInvite.InvitedBy, result.InvitedBy);
        }

        [Fact]
        public async Task CreateNewGuestInviteSetsProjectId()
        {
            var result = await _target.CreateGuestInviteAsync(_defaultGuestInvite, It.IsAny<Guid>());
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.ProjectId);
            Assert.Equal(_defaultGuestInvite.ProjectId, result.ProjectId);
        }

        [Fact]
        public async Task GetGuestInviteCallsGet()
        {
            var id = Guid.NewGuid();
            await _target.GetGuestInviteAsync(id);
            _guestInviteRepositoryMock.Verify(x => x.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()));
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
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .Throws(new NotFoundException("GuestInvite not found"));

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.GetGuestInviteAsync(It.IsAny<Guid>()));
        }

        [Fact]
        public async Task UpdateGuestInviteThrowsNotFoundOnNotFoundException()
        {
            _guestInviteRepositoryMock
                .Setup(x => x.UpdateItemAsync(It.IsAny<Guid>(), _defaultGuestInvite, It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .Throws(new NotFoundException("Message"));

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.UpdateGuestInviteAsync(_defaultGuestInvite));
        }

        [Fact]
        public async Task UpdateGuestInviteVerifyCalled()
        {
            await _target.UpdateGuestInviteAsync(_defaultGuestInvite);
            _guestInviteRepositoryMock.Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestInvite>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task UpdateOfGuestIniviteBussesEvent()
        {
            await _target.UpdateGuestInviteAsync(_defaultGuestInvite);
            _eventServiceMock.Verify(x => x.PublishAsync(It.IsAny<ServiceBusEvent<GuestInvite>>()));
        }

        [Fact]
        public async Task GetValidGuestInvitesByProjectIdThrowsExceptionOnValidationError()
        {
            _validatorMock.Setup(v => v.Validate(It.IsAny<object>()))
                          .Returns(new ValidationResult { Errors = { new ValidationFailure(string.Empty, string.Empty) } });

            await Assert.ThrowsAsync<ValidationFailedException>(() => _target.GetValidGuestInvitesByProjectIdAsync(Guid.Empty));
        }

        [Fact]
        public async Task GetValidGuestInvitesByProjectIdThrowsNotFoundExceptionIfProjectIsNotFound()
        {
            _projectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), null))
                .ReturnsAsync(MicroserviceResponse.Create<Project>(HttpStatusCode.NotFound, new ErrorResponse()));

            await Assert.ThrowsAsync<NotFoundException>(() => _target.GetValidGuestInvitesByProjectIdAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task GetGuestInvitesByProjectIdGetsInvitesForProject()
        {
            var inviteForProjectCount = 3;
            var invites = MakeTestInviteList(_defaultProject.Id, "test@test.com", 0, inviteForProjectCount, 2, 2);

            _projectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), null))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, _defaultProject));

            _guestInviteRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<GuestInvite, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .Returns<Expression<Func<GuestInvite, bool>>, BatchOptions, CancellationToken>((predicate, o, c) =>
                {
                    var expression = predicate.Compile();
                    IEnumerable<GuestInvite> sublist = invites.Where(expression).ToList();
                    return Task.FromResult(sublist);
                });

            var response = await _target.GetValidGuestInvitesByProjectIdAsync(_defaultProject.Id);

            Assert.Equal(inviteForProjectCount, response.Count());
        }

        [Fact]
        public async Task GetValidGuestInvitesByProjectIdFiltersToReturnOneInvitePerEmail()
        {
            var firstEmailInvites = MakeTestInviteList(_defaultProject.Id, "firstEmail@test.com", 2, 0, 0, 0);
            var secondEmailInvites = MakeTestInviteList(_defaultProject.Id, "secondEmail@test.com", 2, 0, 0, 0);
            var thirdEmailInvites = MakeTestInviteList(_defaultProject.Id, "thirdEmail@test.com", 2, 0, 0, 0);

            _projectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>(), null))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, _defaultProject));

            _guestInviteRepositoryMock.Setup(m => m
                .GetItemsAsync(It.IsAny<Expression<Func<GuestInvite, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(firstEmailInvites.Concat(secondEmailInvites.Concat(thirdEmailInvites)));

            var result = await _target.GetValidGuestInvitesByProjectIdAsync(_defaultProject.Id);
            var resultList = result.ToList();

            Assert.Equal(3, resultList.Count);
            Assert.Contains(resultList, invite => invite.GuestEmail == firstEmailInvites.First().GuestEmail);
            Assert.Contains(resultList, invite => invite.GuestEmail == secondEmailInvites.First().GuestEmail);
            Assert.Contains(resultList, invite => invite.GuestEmail == thirdEmailInvites.First().GuestEmail);
        }

        private List<GuestInvite> MakeTestInviteList(Guid projectId,
                                                     string email,
                                                     int matchingProjectIdEmailAccessCodeCount,
                                                     int matchingProjectIdAccessCodeCount,
                                                     int matchingProjectIdEmailCount,
                                                     int nonMatchingProjectCount)
        {
            var invites = new List<GuestInvite>();

            Repeat(matchingProjectIdEmailAccessCodeCount, () =>
            {
                var invite = GuestInvite.Example();
                invite.ProjectId = projectId;
                invite.GuestEmail = email;
                invite.ProjectAccessCode = _defaultProject.GuestAccessCode;
                invite.CreatedDateTime = DateTime.UtcNow;
                invites.Add(invite);
            });

            for (var i = 0; i < matchingProjectIdAccessCodeCount; i++)
            {
                var invite = GuestInvite.Example();
                invite.ProjectId = projectId;
                invite.ProjectAccessCode = _defaultProject.GuestAccessCode;
                invite.GuestEmail = i + email;
                invites.Add(invite);
            }

            Repeat(matchingProjectIdEmailCount, () =>
            {
                var invite = GuestInvite.Example();
                invite.ProjectId = projectId;
                invite.GuestEmail = email;
                invites.Add(invite);
            });

            Repeat(nonMatchingProjectCount, () => { invites.Add(GuestInvite.Example()); });

            return invites;
        }

        [Fact]
        public async Task GetGuestInvitesByUserIdAsyncThrowsIfValidationFails()
        {
            var request = GetGuestInvitesRequest.Example();
            request.GuestEmail = "";
            request.GuestUserId = Guid.Empty;

            _validatorMock
                .Setup(v => v.Validate(It.IsAny<object>()))
                .Returns(new ValidationResult { Errors = { new ValidationFailure(string.Empty, string.Empty) } });

            await Assert.ThrowsAsync<ValidationFailedException>(() => _target.GetGuestInvitesForUserAsync(request));
        }

        [Fact]
        public async Task GetGuestInvitesByUserIdAsyncGetsItems()
        {
            var userId = Guid.NewGuid();
            var invites = new List<GuestInvite> { GuestInvite.Example(), GuestInvite.Example(), GuestInvite.Example() };
            var moreInvites = new List<GuestInvite> { GuestInvite.Example(), GuestInvite.Example(), GuestInvite.Example() };
            invites.ForEach(w => w.UserId = userId);
            invites.AddRange(moreInvites);

            var request = GetGuestInvitesRequest.Example();
            request.GuestUserId = userId;
            request.GuestEmail = "unique@email.com";

            _guestInviteRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<GuestInvite, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .Returns<Expression<Func<GuestInvite, bool>>, BatchOptions, CancellationToken>((predicate, o, c) =>
                {
                    var expression = predicate.Compile();
                    IEnumerable<GuestInvite> sublist = invites.Where(expression).ToList();
                    return Task.FromResult(sublist);
                });

            var result = await _target.GetGuestInvitesForUserAsync(request);

            Assert.True(result != null && result.Count() == invites.Count - moreInvites.Count);
        }

        [Fact]
        public async Task GetGuestInvitesByUserIdReturnsEmptyListForNotInvites()
        {
            var request = GetGuestInvitesRequest.Example();

            var result = await _target.GetGuestInvitesForUserAsync(request);

            Assert.Empty(result);
        }
    }
}