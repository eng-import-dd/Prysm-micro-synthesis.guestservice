using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.Extensions;
using Synthesis.GuestService.InternalApi.Constants;
using Synthesis.GuestService.InternalApi.Enums;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.InternalApi.Requests;
using Synthesis.GuestService.InternalApi.Responses;
using Synthesis.GuestService.Utilities.Interfaces;
using Synthesis.GuestService.Validators;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Api;
using Synthesis.SettingService.InternalApi.Api;
using System;
using System.Collections.Generic;
using System.IdentityModel;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Synthesis.Guest.ProjectContext.Models;
using Synthesis.Guest.ProjectContext.Services;
using Synthesis.Http.Microservice;
using Synthesis.ParticipantService.InternalApi.Services;
using Synthesis.ProjectService.InternalApi.Models;
using Synthesis.Serialization;

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
        private readonly IProjectApi _serviceToServiceProjectApi;
        private readonly ISettingApi _serviceToServiceAccountSettingsApi;
        private readonly IUserApi _userApi;
        private readonly IValidatorLocator _validatorLocator;
        private readonly IProjectLobbyStateController _projectLobbyStateController;
        private readonly IObjectSerializer _synthesisObjectSerializer;
        private readonly IProjectGuestContextService _projectGuestContextService;
        private readonly IRequestHeaders _requestHeaders;

        private const int GuestSessionLimit = 10;

        /// <summary>
        ///     Initializes a new instance of the <see cref="GuestSessionController" /> class.
        /// </summary>
        /// <param name="repositoryFactory">The repository factory.</param>
        /// <param name="validatorLocator">The validator locator.</param>
        /// <param name="eventService">The event service.</param>
        /// <param name="loggerFactory">The logger.</param>
        /// <param name="projectApi"></param>
        /// <param name="serviceToServiceProjectApi"></param>
        /// <param name="projectLobbyStateController"></param>
        /// <param name="userApi"></param>
        /// <param name="emailUtility"></param>
        /// <param name="serviceToServiceAccountSettingsApi">The API for Account/Tenant specific settings</param>
        /// <param name="synthesisObjectSerializer">The Synthesis object serializer</param>
        /// <param name="projectGuestContextService"></param>
        /// <param name="requestHeaders"></param>
        public GuestSessionController(
            IRepositoryFactory repositoryFactory,
            IValidatorLocator validatorLocator,
            IEventService eventService,
            ILoggerFactory loggerFactory,
            IEmailUtility emailUtility,
            IProjectApi projectApi,
            IProjectApi serviceToServiceProjectApi,
            IUserApi userApi,
            IProjectLobbyStateController projectLobbyStateController,
            ISettingApi serviceToServiceAccountSettingsApi,
            IObjectSerializer synthesisObjectSerializer,
            IProjectGuestContextService projectGuestContextService,
            IRequestHeaders requestHeaders)
        {
            _guestSessionRepository = repositoryFactory.CreateRepository<GuestSession>();
            _guestInviteRepository = repositoryFactory.CreateRepository<GuestInvite>();

            _validatorLocator = validatorLocator;
            _eventService = eventService;
            _logger = loggerFactory.GetLogger(this);
            _projectLobbyStateController = projectLobbyStateController;
            _emailUtility = emailUtility;

            _projectApi = projectApi;
            _serviceToServiceProjectApi = serviceToServiceProjectApi;
            _userApi = userApi;
            _serviceToServiceAccountSettingsApi = serviceToServiceAccountSettingsApi;
            _synthesisObjectSerializer = synthesisObjectSerializer;
            _projectGuestContextService = projectGuestContextService;
            _requestHeaders = requestHeaders;
        }

        public async Task<GuestSession> CreateGuestSessionAsync(GuestSession model, Guid principalId)
        {
            var validationResult = _validatorLocator.Validate<GuestSessionValidator>(model);
            if (!validationResult.IsValid)
            {
                _logger.Error("Validation failed while attempting to create a GuestSession resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            await EndGuestSessionsForUser(model.UserId, principalId);

            model.Id = model.Id == Guid.Empty ? Guid.NewGuid() : model.Id;
            model.CreatedDateTime = DateTime.UtcNow;

            if (_requestHeaders.Keys.Contains("SessionIdString"))
            {
                model.SessionId = _requestHeaders["SessionIdString"].FirstOrDefault();
            }
            else if (_requestHeaders.Keys.Contains("SessionId"))
            {
                model.SessionId = _requestHeaders["SessionId"].FirstOrDefault();
            }
            else
            {
                throw new BadRequestException("Request headers do not contain a SessionId");
            }

            var result = await _guestSessionRepository.CreateItemAsync(model);

            // Delete all prior guest sessions with the same UserId and ProjectId as the session just created.
            await _guestSessionRepository.DeleteItemsAsync(x => x.UserId == model.UserId &&
                                                                x.ProjectId == model.ProjectId &&
                                                                x.Id != result.Id);

            await _projectLobbyStateController.RecalculateProjectLobbyStateAsync(model.ProjectId);

            _eventService.Publish(EventNames.GuestSessionCreated, result);

            return result;
        }

        private async Task EndGuestSessionsForUser(Guid userId, Guid principalId)
        {
            var openSessions = (await _guestSessionRepository
                    .GetItemsAsync(g => g.UserId == userId && (g.GuestSessionState == GuestState.InLobby || g.GuestSessionState == GuestState.InProject)))
                    .ToList();

            if (openSessions.Count == 0)
            {
                return;
            }

            var endSessionTasks = openSessions.Select(session =>
                {
                    session.GuestSessionState = GuestState.Ended;

                    return UpdateGuestSessionAsync(session, principalId);
                });

            await Task.WhenAll(endSessionTasks);
        }

        public async Task EndGuestSessionsForProjectAsync(Guid projectId, Guid principalId, bool onlyKickGuestsInProject)
        {
            //var guestSessions = (await _guestSessionRepository.GetItemsAsync(x => x.ProjectId == projectId)).ToList();

            //var guestSessionTasks = guestSessions
            //    .Where(x => onlyKickGuestsInProject && x.GuestSessionState == GuestState.InProject ||
            //        !onlyKickGuestsInProject && x.GuestSessionState != GuestState.Ended)
            //    .Select(session =>
            //    {
            //        session.GuestSessionState = GuestState.Ended;
            //        session.AccessRevokedBy = principalId;
            //        session.AccessRevokedDateTime = DateTime.UtcNow;

            //        _projectGuestContextService.RemoveProjectGuestContextAsync(session.SessionId);
            //        return UpdateGuestSessionAsync(session, principalId);
            //    });

            //await Task.WhenAll(guestSessionTasks);

            var allGuestSessions = (await _guestSessionRepository.GetItemsAsync(x => x.ProjectId == projectId)).ToList();
            var guestSessions = allGuestSessions
                .Where(x => onlyKickGuestsInProject && x.GuestSessionState == GuestState.InProject ||
                    !onlyKickGuestsInProject && x.GuestSessionState != GuestState.Ended);

            foreach (var session in guestSessions)
            {
                session.GuestSessionState = GuestState.Ended;
                session.AccessRevokedBy = principalId;
                session.AccessRevokedDateTime = DateTime.UtcNow;

                await UpdateGuestSessionAsync(session, principalId);
            }

            _eventService.Publish(EventNames.GuestSessionsForProjectDeleted, new GuidEvent(projectId));

            var newState = await _projectLobbyStateController.RecalculateProjectLobbyStateAsync(projectId);

            _eventService.Publish(EventNames.ProjectStatusUpdated, newState);
        }

        public async Task DeleteGuestSessionAsync(Guid id)
        {
            var validationResult = _validatorLocator.Validate<GuestSessionIdValidator>(id);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            try
            {
                var guestSession = await _guestSessionRepository.GetItemAsync(x => x.Id == id);

                await _projectGuestContextService.RemoveProjectGuestContextAsync(guestSession.SessionId);

                await _guestSessionRepository.DeleteItemAsync(id);

                _eventService.Publish(EventNames.GuestSessionDeleted, new GuidEvent(guestSession.Id));

                await _projectLobbyStateController.RecalculateProjectLobbyStateAsync(guestSession.ProjectId);
            }
            catch (DocumentNotFoundException)
            {
                // We don't really care if it's not found.
                // The resource not being there is what we wanted.
            }
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

        public async Task<IEnumerable<GuestSession>> GetMostRecentValidGuestSessionsByProjectIdAsync(Guid projectId)
        {
            var validationResult = _validatorLocator.Validate<ProjectIdValidator>(projectId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the projectId while attempting to retrieve GuestSession resources.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var projectResult = await _serviceToServiceProjectApi.GetProjectByIdAsync(projectId);

            if (!projectResult.IsSuccess() || projectResult.Payload == null)
            {
                var message = $"Failed to retrieve project: {projectId}. Message: {projectResult.ReasonPhrase}, StatusCode: {projectResult.ResponseCode}, ErrorResponse: {_synthesisObjectSerializer.SerializeToString(projectResult.ErrorResponse)}";
                _logger.Error(message);
                throw new NotFoundException(message);
            }

            var project = projectResult.Payload;

            var validGuestSessions = await _guestSessionRepository.GetItemsAsync(x => x.ProjectId == projectId &&
                                                                                      x.ProjectAccessCode == project.GuestAccessCode &&
                                                                                      x.GuestSessionState != GuestState.PromotedToProjectMember);

            if (validGuestSessions == null || !validGuestSessions.Any())
            {
                return new List<GuestSession>();
            }

            var currentValidProjectGuestSessions = validGuestSessions.GroupBy(s => s.UserId).OrderBy(gs => gs.Key).Select(gs => gs.OrderByDescending(x => x.CreatedDateTime).FirstOrDefault());

            return currentValidProjectGuestSessions;
        }

        public async Task<IEnumerable<GuestSession>> GetValidGuestSessionsByProjectIdForCurrentUserAsync(Guid projectId, Guid userId)
        {
            var validationResult = _validatorLocator.ValidateMany(new Dictionary<Type, object>
            {
                { typeof(ProjectIdValidator), projectId },
                { typeof(UserIdValidator), userId }
            });

            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id and/or resource while attempting to retrieve a GuestSession resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var projectResult = await _serviceToServiceProjectApi.GetProjectByIdAsync(projectId);

            if (!projectResult.IsSuccess() || projectResult.Payload == null)
            {
                var message = $"Failed to retrieve project: {projectId}. Message: {projectResult.ReasonPhrase}, StatusCode: {projectResult.ResponseCode}, ErrorResponse: {_synthesisObjectSerializer.SerializeToString(projectResult.ErrorResponse)}";
                _logger.Error(message);
                throw new NotFoundException(message);
            }

            var project = projectResult.Payload;

            var validGuestSessions = await _guestSessionRepository.GetItemsAsync(x => x.ProjectId == projectId &&
                x.UserId == userId &&
                x.ProjectAccessCode == project.GuestAccessCode &&
                x.GuestSessionState != GuestState.PromotedToProjectMember);

            if (validGuestSessions == null || !validGuestSessions.Any())
            {
                return new List<GuestSession>();
            }

            return validGuestSessions.OrderByDescending(x => x.CreatedDateTime);
        }

        public async Task<IEnumerable<GuestSession>> GetGuestSessionsByProjectIdForUserAsync(Guid projectId, Guid userId)
        {
            var validationResult = _validatorLocator.ValidateMany(new Dictionary<Type, object>
            {
                { typeof(ProjectIdValidator), projectId },
                { typeof(UserIdValidator), userId }
            });

            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id and/or resource while attempting to retrieve a GuestSession resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var projectResult = await _serviceToServiceProjectApi.GetProjectByIdAsync(projectId);

            if (!projectResult.IsSuccess() || projectResult.Payload == null)
            {
                var message = $"Failed to retrieve project: {projectId}. Message: {projectResult.ReasonPhrase}, StatusCode: {projectResult.ResponseCode}, ErrorResponse: {_synthesisObjectSerializer.SerializeToString(projectResult.ErrorResponse)}";
                _logger.Error(message);
                throw new NotFoundException(message);
            }

            var project = projectResult.Payload;

            var guestSessions = await _guestSessionRepository.GetItemsAsync(x => x.ProjectId == projectId &&
                x.UserId == userId &&
                x.ProjectAccessCode == project.GuestAccessCode);

            if (guestSessions == null || !guestSessions.Any())
            {
                return new List<GuestSession>();
            }

            return guestSessions.OrderByDescending(x => x.CreatedDateTime);
        }

        public async Task<GuestSession> UpdateGuestSessionAsync(GuestSession guestSessionModel, Guid principalId)
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

            switch (guestSessionModel.GuestSessionState)
            {
                case GuestState.Ended:
                    guestSessionModel.AccessRevokedDateTime = DateTime.UtcNow;
                    guestSessionModel.AccessRevokedBy = principalId;
                    break;

                case GuestState.InProject:
                    guestSessionModel.AccessGrantedDateTime = DateTime.UtcNow;
                    guestSessionModel.AccessGrantedBy = principalId;
                    break;
            }

            var result = await _guestSessionRepository.UpdateItemAsync(guestSessionModel.Id, guestSessionModel);

            if (guestSessionModel.GuestSessionState == GuestState.Ended)
            {
                await _projectGuestContextService.RemoveProjectGuestContextAsync(guestSessionModel.SessionId);
            }
            else
            {
                var existingProjectGuestContext = await _projectGuestContextService.GetProjectGuestContextAsync(result.SessionId);

                var guestState = (Guest.ProjectContext.Enums.GuestState)result.GuestSessionState;

                await _projectGuestContextService.SetProjectGuestContextAsync(new ProjectGuestContext
                {
                    GuestSessionId = existingProjectGuestContext.GuestSessionId,
                    ProjectId = existingProjectGuestContext.ProjectId,
                    GuestState = guestState,
                    TenantId = existingProjectGuestContext.TenantId
                }, result.SessionId);
            }

            _eventService.Publish(EventNames.GuestSessionUpdated, result);

            return result;
        }

        public async Task<GuestVerificationResponse> VerifyGuestAsync(GuestVerificationRequest request, Guid? guestTenantId)
        {
            return await VerifyGuestAsync(request, null, guestTenantId);
        }

        /// <summary>
        /// Verifies the eligibility of a guest to enter a project lobby and potentially join the project.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="project"></param>
        /// <param name="guestTenantId"></param>
        /// <returns></returns>
        /// <remarks>This method is not intended for external use. It is a public interface member to make it available to
        /// the ProjectGuestContextController and for unit tests. The project argument on this method is a optimization to eliminate
        /// a redundant retrieval of the project when this method is invoked by the ProjectGuestContextController.SetProjectGuestContext method.</remarks>
        public async Task<GuestVerificationResponse> VerifyGuestAsync(GuestVerificationRequest request, Project project, Guid? guestTenantId)
        {
            var validationResult = _validatorLocator.Validate<GuestVerificationRequestValidator>(request);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the guest verification request.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var response = new GuestVerificationResponse();

            if (project != null)
            {
                if (request.ProjectId != Guid.Empty && request.ProjectId != project.Id)
                {
                    response.ResultCode = VerifyGuestResponseCode.InvalidCode;
                    response.Message = $"Could not find a project with that project Id.";
                    return response;
                }

                if (request.ProjectAccessCode != project.GuestAccessCode)
                {
                    response.ResultCode = VerifyGuestResponseCode.InvalidCode;
                    response.Message = $"Could not find a project with that project access code.";
                    return response;
                }
            }

            if(project == null)
            {
                if (request.ProjectId != Guid.Empty)
                {
                    var projectResponse = await _serviceToServiceProjectApi.GetProjectByIdAsync(request.ProjectId);
                    if (!projectResponse.IsSuccess())
                    {
                        response.ResultCode = VerifyGuestResponseCode.InvalidCode;
                        response.Message = $"Could not find a project with that project Id.";
                        return response;
                    }

                    project = projectResponse.Payload;
                }
                else
                {
                    var projectResponse = await _serviceToServiceProjectApi.GetProjectByAccessCodeAsync(request.ProjectAccessCode);
                    if (!projectResponse.IsSuccess())
                    {
                        response.ResultCode = VerifyGuestResponseCode.InvalidCode;
                        response.Message = $"Could not find a project with that project access code.";
                        return response;
                    }

                    project = projectResponse.Payload;
                }
            }

            if (project.TenantId == Guid.Empty)
            {
                response.ResultCode = VerifyGuestResponseCode.InvalidCode;
                response.Message = $"There is no tenant associated with this project. Please contact support to fix this project.";
                return response;
            }

            response.ProjectAccessCode = request.ProjectAccessCode;
            response.ProjectName = project.Name;
            response.Username = request.Username;

            var userResponse = await _userApi.GetUserByUserNameOrEmailAsync(request.Username);

            if (!userResponse.IsSuccess())
            {
                if (userResponse.ResponseCode == HttpStatusCode.NotFound)
                {
                    var invite = (await _guestInviteRepository.GetItemsAsync(x => x.GuestEmail == request.Username && x.ProjectAccessCode == request.ProjectAccessCode)).FirstOrDefault();
                    if (!string.IsNullOrEmpty(invite?.GuestEmail))
                    {
                        response.ResultCode = VerifyGuestResponseCode.SuccessNoUser;
                        response.Message = "This user does not exist but has been invited, so can join as a guest";
                        return response;
                    }

                    response.ResultCode = VerifyGuestResponseCode.InvalidNoInvite;
                    response.Message = "This user does not exist and has not been invited";
                    return response;
                }

                _logger.Error($"An error occured while trying to retrieve the user with username: {request.Username}. ResponseCode: {userResponse.ResponseCode}. Reason: {userResponse.ReasonPhrase}");

                response.ResultCode = VerifyGuestResponseCode.Failed;
                response.Message = $"An error occurred while trying to get the user with username: {request.Username}. {userResponse.ReasonPhrase}";
                return response;
            }

            var user = userResponse.Payload;

            if (user.IsLocked)
            {
                response.ResultCode = VerifyGuestResponseCode.UserIsLocked;
                response.Message = "This user is locked";
                return response;
            }

            if (user.IsEmailVerified != true)
            {
                response.ResultCode = VerifyGuestResponseCode.EmailVerificationNeeded;
                response.Message = "The user has not verified his email address";
                return response;
            }

            var isProjectInUsersAccount = guestTenantId == project.TenantId;

            var userSettingsResponse = await _serviceToServiceAccountSettingsApi.GetUserSettingsAsync(project.TenantId);
            var isGuestModeEnableOnProjectAccountSettings = userSettingsResponse.IsSuccess() && userSettingsResponse.Payload.IsGuestModeEnabled;

            if (!isGuestModeEnableOnProjectAccountSettings && !isProjectInUsersAccount)
            {
                response.ResultCode = VerifyGuestResponseCode.Failed;
                response.Message = "Guest mode is not enabled on the account";
                return response;
            }

            if (!project.IsGuestModeEnabled && !isProjectInUsersAccount)
            {
                response.ResultCode = VerifyGuestResponseCode.Failed;
                response.Message = "Guest mode is not enabled on the project";
                return response;
            }

            response.ResultCode = VerifyGuestResponseCode.Success;
            response.Message = "The user may join as a guest";
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

            var project = (await _serviceToServiceProjectApi.GetProjectByAccessCodeAsync(accessCode)).Payload;
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

        public async Task<UpdateGuestSessionStateResponse> UpdateGuestSessionStateAsync(UpdateGuestSessionStateRequest request, Guid principalId)
        {
            var result = new UpdateGuestSessionStateResponse
            {
                ResultCode = UpdateGuestSessionStateResultCodes.Failed
            };

            const string failedToUpdateGuestSession = "Failed to update guest session.";
            GuestSession currentGuestSession;
            try
            {
                currentGuestSession = await _guestSessionRepository.GetItemAsync(request.GuestSessionId);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"{failedToUpdateGuestSession} Failed to get guest session.", ex);
            }

            if (currentGuestSession.GuestSessionState == GuestState.Ended && request.GuestSessionState != GuestState.Ended)
            {
                result.ResultCode = UpdateGuestSessionStateResultCodes.SessionEnded;
                result.Message = $"{failedToUpdateGuestSession} Can\'t update a guest session that is already ended.";
                return result;
            }

            if (currentGuestSession.GuestSessionState == request.GuestSessionState)
            {
                result.ResultCode = UpdateGuestSessionStateResultCodes.SameAsCurrent;
                result.Message = $"{failedToUpdateGuestSession} The guest session is already in that state.";
                result.GuestSession = currentGuestSession;
                return result;
            }

            var projectResponse = await _serviceToServiceProjectApi.GetProjectByIdAsync(currentGuestSession.ProjectId);
            if (projectResponse.ResponseCode == HttpStatusCode.NotFound)
            {
                _logger.Error($"Failed to obtain GuestSession {request.GuestSessionId}. Could not find the project associated with this guest session.");
                result.Message = $"{failedToUpdateGuestSession}  Could not find the project associated with this guest session.";
                return result;
            }

            if (!projectResponse.IsSuccess() || projectResponse?.Payload == null)
            {
                _logger.Error($"Failed to obtain GuestSession {request.GuestSessionId}. Could not retrieve the project associated with this guest session. {projectResponse.ResponseCode} - {projectResponse.ReasonPhrase}");
                result.Message = $"{failedToUpdateGuestSession}  Could not find the project associated with this guest session.";
                return result;
            }

            var project = projectResponse.Payload;
            if (project.GuestAccessCode != currentGuestSession.ProjectAccessCode && request.GuestSessionState != GuestState.Ended)
            {
                result.ResultCode = UpdateGuestSessionStateResultCodes.SessionEnded;
                result.Message = $"{failedToUpdateGuestSession}  Guest access code has changed.";
                return result;
            }

            var availableGuestCount = await GetAvailableGuestCountAsync(currentGuestSession.ProjectId, project.GuestAccessCode);
            if (request.GuestSessionState == GuestState.InProject && availableGuestCount < 1)
            {
                result.ResultCode = UpdateGuestSessionStateResultCodes.ProjectFull;
                result.Message = $"{failedToUpdateGuestSession}  Can\'t admit a guest into a full project.";
                await UpdateProjectLobbyStateAsync(project.Id, LobbyState.GuestLimitReached);
                return result;
            }

            currentGuestSession.GuestSessionState = request.GuestSessionState;

            var guestSession = await UpdateGuestSessionAsync(currentGuestSession, principalId);

            if (guestSession.GuestSessionState == GuestState.InProject && availableGuestCount == 1)
            {
                await UpdateProjectLobbyStateAsync(project.Id, LobbyState.GuestLimitReached);
            }
            else if (currentGuestSession.GuestSessionState == GuestState.InProject && request.GuestSessionState != GuestState.InProject && availableGuestCount == 0)
            {
                await UpdateProjectLobbyStateAsync(project.Id, LobbyState.Normal);
            }

            result.GuestSession = guestSession;
            result.Message = "Guest session state updated.";
            result.ResultCode = UpdateGuestSessionStateResultCodes.Success;

            return result;
        }

        private async Task UpdateProjectLobbyStateAsync(Guid projectId, LobbyState lobbyStatus)
        {
            var projectLobbyState = new ProjectLobbyState { ProjectId = projectId, LobbyState = lobbyStatus };

            await _projectLobbyStateController.UpsertProjectLobbyStateAsync(projectId, projectLobbyState);
        }

        private async Task<int> GetAvailableGuestCountAsync(Guid projectId, string projectAccessCode)
        {
            var codeValidationResult = _validatorLocator.Validate<ProjectAccessCodeValidator>(projectAccessCode);
            if (!codeValidationResult.IsValid)
            {
                _logger.Error($"Failed to validate the project access code {projectAccessCode} for project with Id {projectId}.");
                throw new ValidationFailedException(codeValidationResult.Errors);
            }

            var guestSessionsInProject = await _guestSessionRepository.GetItemsAsync(x => x.ProjectId == projectId && x.ProjectAccessCode == projectAccessCode && x.GuestSessionState == GuestState.InProject);

            return GuestSessionLimit - guestSessionsInProject.Count();
        }
    }
}