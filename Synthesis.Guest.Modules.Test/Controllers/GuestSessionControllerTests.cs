using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.EventBus.Events;
using Synthesis.Guest.ProjectContext.Models;
using Synthesis.Guest.ProjectContext.Services;
using Synthesis.GuestService.InternalApi.Constants;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.InternalApi.Enums;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.Utilities.Interfaces;
using Synthesis.Http.Microservice;
using Synthesis.Http.Microservice.Models;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.InternalApi.Api;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.ProjectService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Models;
using Synthesis.Serialization;
using Synthesis.SettingService.InternalApi.Api;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Controllers
{
    public class GuestSessionControllerTests
    {
        private readonly GuestSessionController _target;
        private readonly Mock<IRepository<GuestSession>> _guestSessionRepositoryMock;
        private readonly Mock<IEventService> _eventServiceMock = new Mock<IEventService>();
        private readonly Mock<IEmailUtility> _emailUtility = new Mock<IEmailUtility>();
        private readonly Mock<IProjectApi> _projectApiMock = new Mock<IProjectApi>();
        private readonly Mock<IProjectApi> _serviceToServiceProjectApiMock = new Mock<IProjectApi>();
        private readonly Mock<ISettingApi> _settingsApiMock = new Mock<ISettingApi>();
        private readonly Mock<IUserApi> _userApiMock = new Mock<IUserApi>();
        private readonly GuestSession _defaultGuestSession = new GuestSession();
        private readonly GuestInvite _defaultGuestInvite = new GuestInvite();
        private readonly Mock<IValidator> _validatorMock = new Mock<IValidator>();
        private readonly Mock<IValidatorLocator> _validatorLocator = new Mock<IValidatorLocator>();
        private readonly Mock<IProjectLobbyStateController> _projectLobbyStateController = new Mock<IProjectLobbyStateController>();
        private readonly Mock<IObjectSerializer> _synthesisObjectSerializer = new Mock<IObjectSerializer>();
        private readonly Mock<IProjectGuestContextService> _projectGuestContextServiceMock = new Mock<IProjectGuestContextService>();
        private readonly Project _defaultProject;

        private static ValidationResult FailedValidationResult => new ValidationResult(
            new List<ValidationFailure>
            {
                new ValidationFailure(string.Empty, string.Empty)
            }
        );

        public GuestSessionControllerTests()
        {
            _defaultProject = new Project
            {
                GuestAccessCode = Guid.NewGuid().ToString(),
                Id = Guid.NewGuid()
            };

            _defaultGuestSession.Id = Guid.NewGuid();
            _defaultGuestSession.UserId = Guid.NewGuid();
            _defaultGuestSession.ProjectId = _defaultProject.Id;
            _defaultGuestSession.ProjectAccessCode = _defaultProject.GuestAccessCode;
            _defaultGuestSession.GuestSessionState = GuestState.InLobby;

            var repositoryFactoryMock = new Mock<IRepositoryFactory>();
            _guestSessionRepositoryMock = new Mock<IRepository<GuestSession>>();
            var guestInviteRepositoryMock = new Mock<IRepository<GuestInvite>>();


            _guestSessionRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_defaultGuestSession);

            _guestSessionRepositoryMock
                .Setup(x => x.CreateItemAsync(It.IsAny<GuestSession>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((GuestSession participant, CancellationToken c) => participant);

            _guestSessionRepositoryMock
                .Setup(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestSession>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, GuestSession participant, UpdateOptions o, CancellationToken c) => participant);

            guestInviteRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_defaultGuestInvite);

            guestInviteRepositoryMock
                .Setup(x => x.CreateItemAsync(It.IsAny<GuestInvite>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((GuestInvite session, CancellationToken c) => session);

            guestInviteRepositoryMock
                .Setup(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestInvite>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, GuestInvite session, UpdateOptions o, CancellationToken c) => session);

            repositoryFactoryMock
#pragma warning disable 612
                .Setup(x => x.CreateRepository<GuestSession>())
#pragma warning restore 612
                .Returns(_guestSessionRepositoryMock.Object);

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

            var sessionIdStringHeader = new KeyValuePair<string, IEnumerable<string>>("SessionIdString", new List<string> { Guid.NewGuid().ToString() });
            var sessionIdHeader = new KeyValuePair<string, IEnumerable<string>>("SessionId", new List<string> { Guid.NewGuid().ToString() });
            var kvpList = new List<KeyValuePair<string, IEnumerable<string>>>(2) { sessionIdStringHeader, sessionIdHeader };
            var headersWithSession = new RequestHeaders(kvpList);

            _target = new GuestSessionController(repositoryFactoryMock.Object, _validatorLocator.Object, _eventServiceMock.Object,
                                                 loggerFactoryMock.Object, _emailUtility.Object, _projectApiMock.Object, _serviceToServiceProjectApiMock.Object,
                                                 _userApiMock.Object, _projectLobbyStateController.Object, _settingsApiMock.Object, _synthesisObjectSerializer.Object,
                                                 _projectGuestContextServiceMock.Object,headersWithSession);
        }

        [Fact]
        public async Task CreateGuestSession_CallsCreate()
        {
            await _target.CreateGuestSessionAsync(_defaultGuestSession);
            _guestSessionRepositoryMock.Verify(x => x.CreateItemAsync(It.IsAny<GuestSession>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task CreateGuestSession_CallsDeleteItemsToClearOldGuestSessionsForUserAndProject()
        {
            await _target.CreateGuestSessionAsync(_defaultGuestSession);

            _guestSessionRepositoryMock.Verify(x => x.DeleteItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task CreateGuestSession_DeletesProjectGuestContextKeysForOldGuestSessionsForUserAndProject()
        {
            var oldGuestSession1 = _defaultGuestSession;
            oldGuestSession1.Id = Guid.NewGuid();

            var oldGuestSession2 = _defaultGuestSession;
            oldGuestSession1.Id = Guid.NewGuid();

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession> { oldGuestSession1, oldGuestSession2 });

            await _target.CreateGuestSessionAsync(_defaultGuestSession);

            _projectGuestContextServiceMock.Verify(x => x.RemoveProjectGuestContextAsync(It.IsAny<string>()), Times.Exactly(2));
        }

        [Fact]
        public async Task CreateGuestSession_ReturnsProvidedGuestSession()
        {
            var result = await _target.CreateGuestSessionAsync(_defaultGuestSession);
            Assert.NotNull(result);
            Assert.Equal(_defaultGuestSession.Id, result.Id);
            Assert.Equal(_defaultGuestSession.UserId, result.UserId);
            Assert.Equal(_defaultGuestSession.ProjectId, result.ProjectId);
            Assert.Equal(_defaultGuestSession.ProjectAccessCode, result.ProjectAccessCode);
        }

        [Fact]
        public async Task CreateGuestSession_CallsRepositoryCreateItemAsync()
        {
            await _target.CreateGuestSessionAsync(_defaultGuestSession);
            _guestSessionRepositoryMock.Verify(x => x.CreateItemAsync(It.IsAny<GuestSession>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task CreateNewGuestSession_BussesEvent()
        {
            await _target.CreateGuestSessionAsync(_defaultGuestSession);
            _eventServiceMock.Verify(x => x.PublishAsync(It.IsAny<ServiceBusEvent<GuestSession>>()));
        }

        [Fact]
        public async Task CreateNewGuestSession_SetsProjectAccessCode()
        {
            var result = await _target.CreateGuestSessionAsync(_defaultGuestSession);
            Assert.NotNull(result);
            Assert.NotEqual(string.Empty, result.ProjectAccessCode);
            Assert.Equal(_defaultGuestSession.ProjectAccessCode, result.ProjectAccessCode);
        }

        [Fact]
        public async Task CreateNewGuestSession_SetsProjectId()
        {
            var result = await _target.CreateGuestSessionAsync(_defaultGuestSession);
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.ProjectId);
            Assert.Equal(_defaultGuestSession.ProjectId, result.ProjectId);
        }

        [Fact]
        public async Task CreateGuestSession_WhenUpdatingExistingGuestSessionsThrowsException_ThrowsException()
        {
            var guestSession = GuestSession.Example();
            guestSession.GuestSessionState = GuestState.InProject;

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession>() { guestSession });

            // Edge case, session record was deleted in-between the time of being retrieved and being updated
            _guestSessionRepositoryMock
                .Setup(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestSession>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .Throws<DocumentNotFoundException>();

            await Assert.ThrowsAnyAsync<Exception>(async () => await _target.CreateGuestSessionAsync(guestSession));
        }

        [Fact]
        public async Task DeleteGuestSessionsForProjectAsync_KillsAllActiveSessions()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession>
                {
                    new GuestSession { GuestSessionState = GuestState.InLobby },
                    new GuestSession { GuestSessionState = GuestState.InProject },
                    new GuestSession { GuestSessionState = GuestState.Ended }
                });

            await _target.DeleteGuestSessionsForProjectAsync(_defaultGuestSession.ProjectId, false);
            _guestSessionRepositoryMock.Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestSession>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task DeleteGuestSessionsForProjectAsync_KillsInProjectSessions()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession>
                {
                    new GuestSession { GuestSessionState = GuestState.InLobby },
                    new GuestSession { GuestSessionState = GuestState.InProject },
                    new GuestSession { GuestSessionState = GuestState.Ended }
                });

            await _target.DeleteGuestSessionsForProjectAsync(_defaultGuestSession.ProjectId, true);
            _guestSessionRepositoryMock.Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestSession>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteGuestSessionsForProjectAsync_DeletesProjectGuestContextKeysForSessions()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession>
                {
                    new GuestSession { GuestSessionState = GuestState.InLobby },
                    new GuestSession { GuestSessionState = GuestState.InProject }
                });

            await _target.DeleteGuestSessionsForProjectAsync(_defaultGuestSession.ProjectId, false);

            _projectGuestContextServiceMock.Verify(x => x.RemoveProjectGuestContextAsync(It.IsAny<string>()), Times.Exactly(2));
        }

        [Fact]
        public async Task DeleteGuestSessionsForProjectAsync_CalculatesProjectLobbyState()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession>
                {
                    new GuestSession { GuestSessionState = GuestState.InLobby },
                    new GuestSession { GuestSessionState = GuestState.InProject },
                    new GuestSession { GuestSessionState = GuestState.Ended }
                });

            await _target.DeleteGuestSessionsForProjectAsync(_defaultGuestSession.ProjectId, true);
            _projectLobbyStateController.Verify(x => x.RecalculateProjectLobbyStateAsync(It.IsAny<Guid>()), Times.Once);
        }

        [Fact]
        public async Task DeleteGuestSessionsForProjectAsync_PublishesGuestSessionsForProjectDeleted()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession>());

            await _target.DeleteGuestSessionsForProjectAsync(_defaultGuestSession.ProjectId, false);

            _eventServiceMock.Verify(x => x.PublishAsync(It.Is<ServiceBusEvent<GuidEvent>>(y => y.Name == EventNames.GuestSessionsForProjectDeleted)));
        }

        [Fact]
        public async Task DeleteGuestSessionsForProjectAsync_PublishesProjectStatusUpdated()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession>());

            await _target.DeleteGuestSessionsForProjectAsync(_defaultGuestSession.ProjectId, false);

            _eventServiceMock.Verify(x => x.PublishAsync(It.Is<ServiceBusEvent<ProjectLobbyState>>(y => y.Name == EventNames.ProjectStatusUpdated)));
        }

        [Fact]
        public async Task EmailHost_WhenGuestSessionThrowsNotFound_ThrowsNotFoundException()
        {
            _userApiMock
                .Setup(x => x.GetUserAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, User.Example()));

            _guestSessionRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new NotFoundException("GuestSession could not be found."));

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.EmailHostAsync(_defaultGuestSession.ProjectAccessCode, Guid.NewGuid()));
        }

        [Fact]
        public async Task EmailHost_WhenGetProjectThrowsNotFound_ThrowsNotFoundException()
        {
            _userApiMock
                .Setup(x => x.GetUserAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, User.Example()));

            _projectApiMock
                .Setup(x => x.GetProjectByAccessCodeAsync(It.IsAny<string>()))
                .ThrowsAsync(new NotFoundException("Project could not be found"));

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.EmailHostAsync(_defaultGuestSession.ProjectAccessCode, Guid.NewGuid()));
        }

        [Fact]
        public async Task EmailHost_WhenGetUserThrowsNotFound_ThrowsNotFoundException()
        {
            _userApiMock
                .Setup(x => x.GetUserAsync(It.IsAny<Guid>()))
                .ThrowsAsync(new NotFoundException("The sending user could not be found"));

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.EmailHostAsync(_defaultGuestSession.ProjectAccessCode, Guid.NewGuid()));
        }

        [Fact]
        public async Task GetGuestSession_CallsRespositoryGetItem()
        {
            var id = Guid.NewGuid();
            await _target.GetGuestSessionAsync(id);
            _guestSessionRepositoryMock.Verify(x => x.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task GetGuestSession_WhenExists_ReturnsGuestSession()
        {
            var result = await _target.GetGuestSessionAsync(Guid.NewGuid());
            Assert.IsType<GuestSession>(result);
        }

        [Fact]
        public async Task GetGuestSession_OnDocumentNotFound_ThrowsNotFound()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.GetItemAsync(It.IsAny<Guid>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .Throws(new NotFoundException("GuestSession could not be found."));

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.GetGuestSessionAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task UpdateGuestSession_OnNotFoundException_ThrowsNotFound()
        {
            _guestSessionRepositoryMock
                .Setup(x => x.UpdateItemAsync(It.IsAny<Guid>(), _defaultGuestSession, It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()))
                .Throws(new NotFoundException("Message"));

            await Assert.ThrowsAsync<NotFoundException>(async () => await _target.UpdateGuestSessionAsync(_defaultGuestSession, It.IsAny<Guid>()));
        }

        [Fact]
        public async Task UpdateGuestSession_CallsRepositoryUpdateItem()
        {
            _projectGuestContextServiceMock.Setup(x => x.GetProjectGuestContextAsync(It.IsAny<string>())).ReturnsAsync(new ProjectGuestContext
            {
                GuestSessionId = _defaultGuestSession.Id,
                GuestState = Guest.ProjectContext.Enums.GuestState.InLobby,
                ProjectId = _defaultGuestSession.ProjectId,
                TenantId = Guid.NewGuid()
            });

            await _target.UpdateGuestSessionAsync(_defaultGuestSession, It.IsAny<Guid>());

            _guestSessionRepositoryMock.Verify(x => x.UpdateItemAsync(It.IsAny<Guid>(), It.IsAny<GuestSession>(), It.IsAny<UpdateOptions>(), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task UpdateGuestSession_BussesEvent()
        {
            _projectGuestContextServiceMock.Setup(x => x.GetProjectGuestContextAsync(It.IsAny<string>())).ReturnsAsync(new ProjectGuestContext
            {
                GuestSessionId = _defaultGuestSession.Id,
                GuestState = Guest.ProjectContext.Enums.GuestState.InLobby,
                ProjectId = _defaultGuestSession.ProjectId,
                TenantId = Guid.NewGuid()
            });

            await _target.UpdateGuestSessionAsync(_defaultGuestSession, It.IsAny<Guid>());

            _eventServiceMock.Verify(x => x.PublishAsync(It.IsAny<ServiceBusEvent<GuestSession>>()));
        }

        [Fact]
        public async Task UpdateGuestSession_UpdatesProjectGuestContextInRedis()
        {
            _projectGuestContextServiceMock.Setup(x => x.GetProjectGuestContextAsync(It.IsAny<string>())).ReturnsAsync(new ProjectGuestContext
            {
                GuestSessionId = _defaultGuestSession.Id,
                GuestState = Guest.ProjectContext.Enums.GuestState.InLobby,
                ProjectId = _defaultGuestSession.ProjectId,
                TenantId = Guid.NewGuid()
            });

            await _target.UpdateGuestSessionAsync(_defaultGuestSession, Guid.NewGuid());
            _projectGuestContextServiceMock.Verify(x => x.SetProjectGuestContextAsync(It.IsAny<ProjectGuestContext>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task UpdateGuestSessionState_IfProjectWithInvalidGuestAccessCodeIsReturned_ThrowsValidationException()
        {
            _validatorMock
                .Setup(v => v.Validate(It.IsAny<object>()))
                .Returns(FailedValidationResult);

            _projectApiMock.Setup(x => x.GetProjectByIdAsync(_defaultGuestSession.ProjectId))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, new Project
                {
                    Id = _defaultGuestSession.ProjectId,
                    GuestAccessCode = "INVALID"
                }));

            await Assert.ThrowsAsync<ValidationFailedException>(async () => await _target.UpdateGuestSessionStateAsync(new UpdateGuestSessionStateRequest
            {
                GuestSessionId = _defaultGuestSession.Id,
                GuestSessionState = GuestState.Ended
            }, It.IsAny<Guid>()));
        }

        [Fact]
        public async Task GetMostRecentValidGuestSessionsByProjectId_IfProjectNotFound_ThrowsNotFoundException()
        {
            _serviceToServiceProjectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create<Project>(HttpStatusCode.NotFound, new ErrorResponse()));

            await Assert.ThrowsAsync<NotFoundException>(async () =>
                await _target.GetMostRecentValidGuestSessionsByProjectIdAsync(_defaultGuestSession.ProjectId));
        }

        [Fact]
        public async Task GetMostRecentValidGuestSessionsByProjectId_ReturnsSessionsMatchingProjectIdAndAccessCodeAndNotPromotedToProjectMember()
        {
            var expectedReturnedGuestSession = new GuestSession
            {
                ProjectId = _defaultProject.Id,
                ProjectAccessCode = _defaultProject.GuestAccessCode,
                UserId = Guid.NewGuid()
            };

            var guestSessions = new List<GuestSession>
            {
                expectedReturnedGuestSession,
                new GuestSession
                {
                    ProjectId = _defaultProject.Id,
                    ProjectAccessCode = Guid.NewGuid().ToString(),
                    UserId = Guid.NewGuid()
                },
                new GuestSession
                {
                    ProjectId = Guid.NewGuid(),
                    ProjectAccessCode = _defaultProject.GuestAccessCode,
                    UserId = Guid.NewGuid()
                },
                new GuestSession
                {
                    ProjectId = _defaultProject.Id,
                    ProjectAccessCode = _defaultProject.GuestAccessCode,
                    UserId = Guid.NewGuid(),
                    GuestSessionState = GuestState.PromotedToProjectMember
                }
            };

            _serviceToServiceProjectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, _defaultProject));

            _guestSessionRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .Returns<Expression<Func<GuestSession, bool>>, BatchOptions, CancellationToken>((predicate, bo, ct) =>
                {
                    var expression = predicate.Compile();
                    IEnumerable<GuestSession> sublist = guestSessions.Where(expression).ToList();
                    return Task.FromResult(sublist);
                });

            var result = await _target.GetMostRecentValidGuestSessionsByProjectIdAsync(_defaultProject.Id);
            var resultList = result.ToList();

            Assert.Single(resultList);
            Assert.Contains(expectedReturnedGuestSession, resultList);
        }

        [Fact]
        public async Task GetMostRecentValidGuestSessionsByProjectId_IfNoSessionsFoundReturns_EmptyList()
        {
            _serviceToServiceProjectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, _defaultProject));

            _guestSessionRepositoryMock.Setup(x => x
                .GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession>());

            var result = await _target.GetMostRecentValidGuestSessionsByProjectIdAsync(_defaultGuestSession.ProjectId);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetMostRecentValidGuestSessionsByProjectId_FiltersResultsToReturnMostRecentSessionForEachUniqueUserId()
        {
            var userId1 = Guid.NewGuid();
            var userId2 = Guid.NewGuid();
            var userId3 = Guid.NewGuid();

            var shouldBeReturned = new List<GuestSession>
            {
                new GuestSession { CreatedDateTime = new DateTime(2018, 7, 26), UserId = userId1 },
                new GuestSession { CreatedDateTime = new DateTime(2018, 7, 26), UserId = userId2 },
                new GuestSession { CreatedDateTime = new DateTime(2018, 7, 26), UserId = userId3 }
            };

            var shouldNotBeReturned = new List<GuestSession>
            {
                new GuestSession { CreatedDateTime = new DateTime(2018, 7, 25), UserId = userId1 },
                new GuestSession { CreatedDateTime = new DateTime(2018, 7, 25), UserId = userId2 },
                new GuestSession { CreatedDateTime = new DateTime(2018, 7, 25), UserId = userId3 }
            };

            _serviceToServiceProjectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, _defaultProject));

            _guestSessionRepositoryMock.Setup(x => x
                .GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(shouldBeReturned.Concat(shouldNotBeReturned));

            var result = await _target.GetMostRecentValidGuestSessionsByProjectIdAsync(_defaultGuestSession.ProjectId);

            Assert.All(shouldBeReturned, session => Assert.Contains(session, result));
            Assert.All(shouldNotBeReturned, session => Assert.DoesNotContain(session, result));
        }

        [Fact]
        public async Task GetValidGuestSessionsByProjectIdForCurrentUser_WhenProjectNotFound_ThrowsNotFoundException()
        {
            _serviceToServiceProjectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create<Project>(HttpStatusCode.NotFound, new ErrorResponse()));

            await Assert.ThrowsAsync<NotFoundException>(async () =>
                await _target.GetValidGuestSessionsByProjectIdForCurrentUserAsync(_defaultGuestSession.ProjectId, Guid.NewGuid()));
        }

        [Fact]
        public async Task GetValidGuestSessionsByProjectIdForCurrentUser_WhenNoSessionsFound_ReturnsEmpty()
        {
            _serviceToServiceProjectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, _defaultProject));

            _guestSessionRepositoryMock.Setup(x => x
                    .GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GuestSession>());

            var result = await _target.GetValidGuestSessionsByProjectIdForCurrentUserAsync(_defaultGuestSession.ProjectId, Guid.NewGuid());

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetValidGuestSessionsByProjectIdForCurrentUser_ReturnsItemsMatchingQueryWhereClause()
        {
            var expectedUserId = Guid.NewGuid();

            var guestExpectedGuestSessions = new List<GuestSession>
            {
                new GuestSession
                {
                    ProjectId = _defaultProject.Id,
                    ProjectAccessCode = _defaultProject.GuestAccessCode,
                    UserId = expectedUserId,
                    CreatedDateTime = DateTime.UtcNow
                },
                new GuestSession
                {
                    ProjectId = _defaultProject.Id,
                    ProjectAccessCode = _defaultProject.GuestAccessCode,
                    UserId = expectedUserId,
                    CreatedDateTime = DateTime.UtcNow.AddHours(-2.0)
                },
                new GuestSession
                {
                    ProjectId = _defaultProject.Id,
                    ProjectAccessCode = _defaultProject.GuestAccessCode,
                    UserId = expectedUserId,
                    CreatedDateTime = DateTime.UtcNow.AddDays(-2.0)
                },
            };

            var notExpectedGuestSessions = new List<GuestSession>
            {
                new GuestSession
                {
                    ProjectId = _defaultProject.Id,
                    ProjectAccessCode = Guid.NewGuid().ToString(),
                    UserId = Guid.NewGuid()
                },
                new GuestSession
                {
                    ProjectId = Guid.NewGuid(),
                    ProjectAccessCode = _defaultProject.GuestAccessCode,
                    UserId = Guid.NewGuid()
                },
                new GuestSession
                {
                    ProjectId = _defaultProject.Id,
                    ProjectAccessCode = _defaultProject.GuestAccessCode,
                    UserId = Guid.NewGuid(),
                    GuestSessionState = GuestState.PromotedToProjectMember
                }
            };

            var guestSessions = notExpectedGuestSessions.Concat(guestExpectedGuestSessions).ToList();

            _serviceToServiceProjectApiMock.Setup(x => x.GetProjectByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(MicroserviceResponse.Create(HttpStatusCode.OK, _defaultProject));

            _guestSessionRepositoryMock.Setup(m => m.GetItemsAsync(It.IsAny<Expression<Func<GuestSession, bool>>>(), It.IsAny<BatchOptions>(), It.IsAny<CancellationToken>()))
                .Returns<Expression<Func<GuestSession, bool>>, BatchOptions, CancellationToken>((predicate, bo, ct) =>
                {
                    var expression = predicate.Compile();
                    IEnumerable<GuestSession> sublist = guestSessions.Where(expression).ToList();
                    return Task.FromResult(sublist);
                });

            var result = await _target.GetValidGuestSessionsByProjectIdForCurrentUserAsync(_defaultProject.Id, expectedUserId);
            var resultList = result.ToList();

            Assert.Collection(resultList,
                item => Assert.Equal(guestExpectedGuestSessions[0], item),
                item => Assert.Equal(guestExpectedGuestSessions[1], item),
                item => Assert.Equal(guestExpectedGuestSessions[2], item)
            );
        }
    }
}