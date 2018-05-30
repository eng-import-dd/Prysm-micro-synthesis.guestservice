using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.ApiWrappers.Interfaces;
using Synthesis.GuestService.Extensions;
using Synthesis.GuestService.InternalApi.Constants;
using Synthesis.GuestService.InternalApi.Enums;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.InternalApi.Responses;
using Synthesis.GuestService.Utilities.Interfaces;
using Synthesis.GuestService.Validators;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Synthesis.Http.Microservice;

namespace Synthesis.GuestService.Controllers
{
    /// <summary>
    ///     Represents a controller for GuestSession resources.
    /// </summary>
    /// <seealso cref="IGuestSessionController" />
    public class GuestSessionController : IGuestSessionController
    {
        private readonly IEmailUtility _emailUtility;
        private readonly IEventService _eventService;
        private readonly IRepository<GuestSession> _guestSessionRepository;
        private readonly IRepository<GuestInvite> _guestInviteRepository;
        private readonly ILogger _logger;
        private readonly IProjectApi _projectApi;
        private readonly ISettingsApiWrapper _settingsApi;
        private readonly IUserApi _userApi;
        private readonly IValidatorLocator _validatorLocator;
        private readonly IProjectLobbyStateController _projectLobbyStateController;

        /// <summary>
        ///     Initializes a new instance of the <see cref="GuestSessionController" /> class.
        /// </summary>
        /// <param name="repositoryFactory">The repository factory.</param>
        /// <param name="validatorLocator">The validator locator.</param>
        /// <param name="eventService">The event service.</param>
        /// <param name="loggerFactory">The logger.</param>
        /// <param name="projectApi"></param>
        /// <param name="projectLobbyStateController"></param>
        /// <param name="settingsApi"></param>
        /// <param name="userApi"></param>
        /// <param name="emailUtility"></param>
        public GuestSessionController(
            IRepositoryFactory repositoryFactory,
            IValidatorLocator validatorLocator,
            IEventService eventService,
            ILoggerFactory loggerFactory,
            IEmailUtility emailUtility,
            IProjectApi projectApi,
            IUserApi userApi,
            IProjectLobbyStateController projectLobbyStateController,
            ISettingsApiWrapper settingsApi)
        {
            _guestSessionRepository = repositoryFactory.CreateRepository<GuestSession>();
            _guestInviteRepository = repositoryFactory.CreateRepository<GuestInvite>();

            _validatorLocator = validatorLocator;
            _eventService = eventService;
            _logger = loggerFactory.GetLogger(this);
            _projectLobbyStateController = projectLobbyStateController;
            _emailUtility = emailUtility;

            _projectApi = projectApi;
            _userApi = userApi;
            _settingsApi = settingsApi;
        }

        public async Task<GuestSession> CreateGuestSessionAsync(GuestSession model)
        {
            var validationResult = _validatorLocator.Validate<GuestSessionValidator>(model);
            if (!validationResult.IsValid)
            {
                _logger.Error("Validation failed while attempting to create a GuestSession resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            await EndGuestSessionsForUser(model.UserId);

            model.Id = model.Id == Guid.Empty ? Guid.NewGuid() : model.Id;
            model.CreatedDateTime = DateTime.UtcNow;
            var result = await _guestSessionRepository.CreateItemAsync(model);

            _eventService.Publish(EventNames.GuestSessionCreated, result);

            return result;
        }

        private async Task EndGuestSessionsForUser(Guid userId)
        {
            var openSessions = (await _guestSessionRepository
                    .GetItemsAsync(g => g.UserId == userId && g.GuestSessionState != GuestState.Ended))
                    .ToList();

            if (openSessions.Count == 0)
            {
                return;
            }

            var endSessionTasks = openSessions.Select(guestSession =>
            {
                return new Task(async () =>
                {
                    guestSession.GuestSessionState = GuestState.Ended;
                    await _guestSessionRepository.UpdateItemAsync(guestSession.Id, guestSession);
                });
            });

            await Task.WhenAll(endSessionTasks);
        }


        public async Task DeleteGuestSessionsForProjectAsync(Guid projectId, bool onlyKickGuestsInProject)
        {
            var guestSessions = (await _guestSessionRepository.GetItemsAsync(x => x.ProjectId == projectId)).ToList();

            var guestSessionTasks = guestSessions
                .Where(x => onlyKickGuestsInProject && x.GuestSessionState == GuestState.InProject ||
                    !onlyKickGuestsInProject && x.GuestSessionState != GuestState.Ended)
                .Select(session =>
                {
                    session.GuestSessionState = GuestState.Ended;
                    return _guestSessionRepository.UpdateItemAsync(session.Id, session);
                });

            await Task.WhenAll(guestSessionTasks);

            _eventService.Publish(EventNames.GuestSessionsForProjectDeleted, new GuidEvent(projectId));
        }

        public async Task<GuestSession> GetGuestSessionAsync(Guid id)
        {
            var validationResult = _validatorLocator.Validate<GuestSessionIdValidator>(id);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id while attempting to retrieve a GuestSession resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var result = await _guestSessionRepository.GetItemAsync(id);
            if (result != null)
            {
                return result;
            }

            _logger.Error($"A GuestSession resource could not be found for id {id}");
            throw new NotFoundException("GuestSession could not be found");
        }

        public async Task<IEnumerable<GuestSession>> GetGuestSessionsByProjectIdAsync(Guid projectId)
        {
            var validationResult = _validatorLocator.Validate<ProjectIdValidator>(projectId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the projectId while attempting to retrieve GuestSession resources.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var result = await _guestSessionRepository.GetItemsAsync(x => x.ProjectId == projectId);
            if (result != null)
            {
                return result;
            }

            _logger.Error($"GuestSession resources could not be found for projectId {projectId}");
            throw new NotFoundException("GuestSessions could not be found");
        }

        public async Task<GuestSession> UpdateGuestSessionAsync(GuestSession guestSessionModel)
        {
            var validationResult = _validatorLocator.ValidateMany(new Dictionary<Type, object>
            {
                { typeof(GuestSessionIdValidator), guestSessionModel.Id },
                { typeof(GuestSessionValidator), guestSessionModel }
            });

            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id and/or resource while attempting to update a GuestSession resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var result = await _guestSessionRepository.UpdateItemAsync(guestSessionModel.Id, guestSessionModel);

            _eventService.Publish(EventNames.GuestSessionUpdated, result);

            return result;
        }

        public async Task<GuestVerificationResponse> VerifyGuestAsync(string username, string projectAccessCode)
        {
            return await VerifyGuestAsync(username, Guid.Empty, projectAccessCode);
        }

        public async Task<GuestVerificationResponse> VerifyGuestAsync(string username, Guid projectId, string projectAccessCode)
        {
            var validationResult = _validatorLocator.ValidateMany(new Dictionary<Type, object>
            {
                { typeof(EmailValidator), username },
                { typeof(ProjectAccessCodeValidator), projectAccessCode }
            });

            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the guest verification request.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var response = new GuestVerificationResponse();
            var projectResponse = await _projectApi.GetProjectByAccessCodeAsync(projectAccessCode);
            if (projectResponse == null)
            {
                response.ResultCode = VerifyGuestResponseCode.Failed;
                return response;
            }

            var project = projectResponse.Payload;

            response.AccountId = project.TenantId;
            response.AssociatedProjectId = project.Id;
            response.ProjectAccessCode = projectAccessCode;
            response.ProjectName = project.Name;
            response.UserId = project.OwnerId;
            response.Username = username;

            if (project.IsGuestModeEnabled != true)
            {
                response.ResultCode = VerifyGuestResponseCode.Failed;
                return response;
            }

            var userResponse = await _userApi.GetUserByUsernameAsync(username);
            if (userResponse == null)
            {
                response.ResultCode = VerifyGuestResponseCode.Failed;
                return response;
            }

            var user = userResponse.Payload;

            if (user.IsLocked)
            {
                response.ResultCode = VerifyGuestResponseCode.UserIsLocked;
                return response;
            }

            // TODO: IsEmailVerified is no longer supplied in the user object returned by the microservice.  Needs to be obtained from elsewhere
            //if (user.IsEmailVerified != true)
            //{
            //    response.ResultCode = VerifyGuestResponseCode.EmailVerificationNeeded;
            //    return response;
            //}

            // TODO: TenantId is no longer part of the User model.  This is in-flux due to try-n-buy and needs to be thought thru
            //if (user.TenantId != null)
            //{
            //    response.ResultCode = VerifyGuestResponseCode.InvalidNotGuest;
            //    return response;
            //}

            var settingsResponse = await _settingsApi.GetSettingsAsync(user.Id.GetValueOrDefault());
            if (settingsResponse != null)
            {
                var settings = settingsResponse.Payload;
                if (settings.IsGuestModeEnabled != true)
                {
                    response.ResultCode = VerifyGuestResponseCode.Failed;
                    return response;
                }
            }

            response.ResultCode = VerifyGuestResponseCode.Success;
            return response;
        }

        public async Task<SendHostEmailResponse> EmailHostAsync(string accessCode, Guid sendingUserId)
        {
            var codeValidationResult = _validatorLocator.Validate<ProjectAccessCodeValidator>(accessCode);
            if (!codeValidationResult.IsValid)
            {
                _logger.Error($"Failed to validate the project access code {accessCode} while attempting to email the host");
                throw new ValidationFailedException(codeValidationResult.Errors);
            }

            var sendingUser = (await _userApi.GetUserAsync(sendingUserId)).Payload;
            if (sendingUser == null)
            {
                throw new NotFoundException($"User with id {sendingUserId} could not be found");
            }

            var userSession = (await _guestSessionRepository.GetItemsAsync(x => x.UserId == sendingUserId && x.ProjectAccessCode == accessCode)).FirstOrDefault();
            if (userSession == null)
            {
                throw new NotFoundException($"User guest session with userId {sendingUserId} and project access code {accessCode} could not be found");
            }

            var project = (await _projectApi.GetProjectByAccessCodeAsync(accessCode)).Payload;
            if (project == null)
            {
                throw new NotFoundException($"Project with access code {accessCode} could not be found");
            }

            var projectOwner = (await _userApi.GetUserAsync(project.OwnerId)).Payload;
            if (projectOwner == null)
            {
                throw new NotFoundException($"User for project owner {project.OwnerId} could not be found");
            }

            if (userSession.EmailedHostDateTime != null)
            {
                throw new Exception($"User {sendingUser.Email} has already emailed the host {projectOwner.Email} once for this guest session {userSession.Id}");
            }

            var invite = (await _guestInviteRepository.GetItemsAsync(x => x.UserId == sendingUserId && x.ProjectAccessCode == accessCode)).FirstOrDefault();
            var sendTo = invite == null ? projectOwner.Email : invite.GuestEmail;

            if (!_emailUtility.SendHostEmail(sendTo, sendingUser.GetFullName(), sendingUser.FirstName, sendingUser.Email, project.Name))
            {
                throw new Exception($"Email from user {sendingUser.Email} to host {projectOwner.Email} could not be sent");
            }

            userSession.EmailedHostDateTime = DateTime.UtcNow;
            await _guestSessionRepository.UpdateItemAsync(userSession.Id, userSession);

            return new SendHostEmailResponse()
            {
                EmailSentDateTime = DateTime.UtcNow,
                SentBy = sendingUser.Email
            };
        }

        public async Task<UpdateGuestSessionStateResponse> UpdateGuestSessionStateAsync(UpdateGuestSessionStateRequest request)
        {
            var result = new UpdateGuestSessionStateResponse
            {
                ResultCode = UpdateGuestSessionStateResultCodes.Failed
            };
            const string failedToUpdateGuestSession = "Failed to update guest session. ";
            try
            {
                GuestSession currentGuestSession;
                try
                {
                    currentGuestSession = await _guestSessionRepository.GetItemAsync(request.GuestSessionId);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to obtain GuestSession {request.GuestSessionId}", ex);
                    result.Message = $"{failedToUpdateGuestSession} Failed to get guest session, {ex.Message}";
                    return result;
                }

                if (currentGuestSession.GuestSessionState == GuestState.Ended && request.GuestSessionState != GuestState.Ended)
                {
                    result.ResultCode = UpdateGuestSessionStateResultCodes.SessionEnded;
                    result.Message = $"{failedToUpdateGuestSession} Can\'t update a guest session that is already ended";
                    return result;
                }

                if (currentGuestSession.GuestSessionState == request.GuestSessionState)
                {
                    result.ResultCode = UpdateGuestSessionStateResultCodes.SameAsCurrent;
                    result.Message = $"{failedToUpdateGuestSession} The guest session is already in that state";
                    result.GuestSession = currentGuestSession;
                    return result;
                }

                var projectResponse = await _projectApi.GetProjectByIdAsync(currentGuestSession.ProjectId);
                if (projectResponse.ResponseCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.Error($"Failed to obtain GuestSession {request.GuestSessionId}. Could not find the project associated with this guest session");
                    result.Message = $"{failedToUpdateGuestSession} ";
                    return result;
                }
                if (!projectResponse.IsSuccess())
                {
                    _logger.Error($"Failed to obtain GuestSession {request.GuestSessionId}. {projectResponse.ResponseCode} - {projectResponse.ReasonPhrase}");
                    return result;
                }

                var project = projectResponse.Payload;
                if (project.GuestAccessCode != currentGuestSession.ProjectAccessCode && request.GuestSessionState != GuestState.Ended)
                {
                    result.ResultCode = UpdateGuestSessionStateResultCodes.SessionEnded;
                    return result;
                }

                var availableGuestCount = await GetAvailableGuestCountAsync(currentGuestSession.ProjectId);
                if (request.GuestSessionState == GuestState.InProject && availableGuestCount < 1)
                {
                    result.ResultCode = UpdateGuestSessionStateResultCodes.ProjectFull;

                    await UpdateProjectLobbyStateAsync(project.Id, LobbyState.GuestLimitReached);
                    return result;
                }

                currentGuestSession.GuestSessionState = request.GuestSessionState;

                var guestSession = await UpdateGuestSessionAsync(currentGuestSession);

                if (guestSession.GuestSessionState == GuestState.InProject && availableGuestCount == 1)
                {
                    await UpdateProjectLobbyStateAsync(project.Id, LobbyState.GuestLimitReached);
                }
                else if (currentGuestSession.GuestSessionState == GuestState.InProject && request.GuestSessionState != GuestState.InProject && availableGuestCount == 0)
                {
                    await UpdateProjectLobbyStateAsync(project.Id, LobbyState.Normal);
                }

                result.GuestSession = guestSession;
                result.Message = "Guest session state updated";
                result.ResultCode = UpdateGuestSessionStateResultCodes.Success;
            }
            catch (Exception ex)
            {
                result.ResultCode = UpdateGuestSessionStateResultCodes.Failed;
                result.Message = $"{failedToUpdateGuestSession} {ex.Message}";
                _logger.Error($"Error updating Guest Session State for guest session {request.GuestSessionId}", ex);
            }

            return result;
        }

        private async Task UpdateProjectLobbyStateAsync(Guid projectId, LobbyState lobbyStatus)
        {
            var projectLobbyState = new ProjectLobbyState { ProjectId = projectId,  LobbyState = lobbyStatus };

            await _projectLobbyStateController.UpsertProjectLobbyStateAsync(projectId, projectLobbyState);
        }

        private async Task<int> GetAvailableGuestCountAsync(Guid projectId)
        {
            var guestSessionsInProject = await _guestSessionRepository.GetItemsAsync(x => x.ProjectId == projectId);

            return 10 - guestSessionsInProject.Count(g => g.GuestSessionState == GuestState.InProject);
        }
    }
}