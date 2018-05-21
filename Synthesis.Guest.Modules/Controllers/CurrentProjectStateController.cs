using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.GuestService.Extensions;
using Synthesis.GuestService.InternalApi.Constants;
using Synthesis.GuestService.InternalApi.Enums;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.ProjectService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Models;

namespace Synthesis.GuestService.Controllers
{
    public class CurrentProjectStateController
    {
        private readonly IEventService _eventService;
        private readonly IRepository<GuestSession> _guestSessionRepository;
        private readonly ILogger _logger;
        private readonly IProjectApi _projectApi;
        private readonly IValidatorLocator _validatorLocator;
        private readonly IGuestUserProjectSessionService _guestUserProjectStateService;
        private readonly IGuestSessionController _guestSessionController;
        private readonly IProjectLobbyStateController _projectLobbyStateController;
        private readonly IProjectAccessApi _projectAccessApi;

        /// <summary>
        ///     Initializes a new instance of the <see cref="GuestSessionController" /> class.
        /// </summary>
        /// <param name="repositoryFactory">The repository factory.</param>
        /// <param name="validatorLocator">The validator locator.</param>
        /// <param name="eventService">The event service.</param>
        /// <param name="loggerFactory">The logger.</param>
        /// <param name="projectApi"></param>
        /// <param name="guestSessionController"></param>
        /// <param name="projectAccessApi"></param>
        /// <param name="guestUserProjectSessionService"></param>
        public CurrentProjectStateController(
            IRepositoryFactory repositoryFactory,
            IValidatorLocator validatorLocator,
            IEventService eventService,
            ILoggerFactory loggerFactory,
            IProjectApi projectApi,
            IGuestSessionController guestSessionController,
            IProjectAccessApi projectAccessApi,
            IProjectLobbyStateController projectLobbyStateController,
            IGuestUserProjectSessionService guestUserProjectSessionService)
        {
            _guestSessionRepository = repositoryFactory.CreateRepository<GuestSession>();

            _validatorLocator = validatorLocator;
            _eventService = eventService;
            _logger = loggerFactory.GetLogger(this);
            _guestSessionController = guestSessionController;
            _projectLobbyStateController = projectLobbyStateController;

            _projectApi = projectApi;
            _projectAccessApi = projectAccessApi;
            _guestUserProjectStateService = guestUserProjectSessionService;
        }
        public async Task<CurrentProjectState> SetCurrentProject(Guid projectId, string accessCode, string sessionId, Guid currentUserId)
        {
            var guestUserState = await _guestUserProjectStateService.GetGuestUserStateAsync();
            if (projectId.Equals(Guid.Empty))
            {
                if (guestUserState.GuestSessionId == Guid.Empty)
                {
                    return new CurrentProjectState()
                    {
                        Message = "No guest session from which to clear the current project."
                    };
                }

                var guestSessionRequest = new UpdateGuestSessionStateRequest()
                {
                    GuestSessionId = guestUserState.GuestSessionId,
                    GuestSessionState = GuestState.Ended
                };

                var guestSessionStateResponse = await UpdateGuestSessionStateAsync(guestSessionRequest);


                guestUserState = new GuestProjectState();
                await _guestUserProjectStateService.SetGuestUserStateAsync(guestUserState);

                if (guestSessionStateResponse.ResultCode == UpdateGuestSessionStateResultCodes.Success)
                {
                    return new CurrentProjectState()
                    {
                        Message = "Cleared current project and ended guest session."
                    };
                }
                return new CurrentProjectState()
                {
                    Message = $"Could not clear the current project or end the guest session. {guestSessionStateResponse.Message}"
                };
            }

            var projectUsersTask =  _projectAccessApi.GetUserIdsByProjectAsync(projectId);
            var projectResponse = await _projectApi.GetProjectByIdAsync(projectId);
            var projectUsersResponse = await projectUsersTask;

            if (!projectResponse.IsSuccess() && projectResponse.ResponseCode != HttpStatusCode.Forbidden)
            {
                throw new Exception($"Error fetching project {projectId}, {projectResponse.ResponseCode} - {projectResponse.ReasonPhrase}");
            }
            if (!projectUsersResponse.IsSuccess())
            {
                throw new Exception($"Error fetching project users {projectId}, {projectUsersResponse.ResponseCode} - {projectUsersResponse.ReasonPhrase}");
            }

            var projectUsers = projectUsersResponse.Payload;
            var project = projectResponse.Payload;
            var userIsProjectMember = projectUsers.Any(userId => userId == currentUserId);
            var isGuest = await _guestUserProjectStateService.IsGuestAsync();

            if (isGuest && userIsProjectMember)
            {
                //User is in project's account and was a guest who was promoted to a full
                //member, clear guest properties. This changes the value of IsGuest.
                await _guestUserProjectStateService.SetGuestUserStateAsync(new GuestProjectState());
            }

            bool userHasAccess;

            if (isGuest)
            {
                userHasAccess = await DetermineGuestAccessAsync(currentUserId, projectId);
            }
            else
            {
                userHasAccess = projectResponse.ResponseCode != HttpStatusCode.Forbidden;
            }

            return new CurrentProjectState();
        }

        private async Task<CurrentProjectState> SetCurrentProjectStateResponse(Project project, bool userHasAccess)
        {
            LobbyState lobbyState;

            var lobbyStateTask = _projectLobbyStateController.GetProjectLobbyStateAsync(project.Id);
            var guestState = await _guestUserProjectStateService.GetGuestUserStateAsync();

            try
            {
                var lobbyStateResponse = await lobbyStateTask;
                lobbyState = lobbyStateResponse.LobbyState;
            }
            catch (Nancy.MicroService.NotFoundException)
            {
                lobbyState = LobbyState.Undefined;
            }
            
            var guestSession = guestState == null || guestState.GuestSessionId == Guid.Empty
                ? null
                : await _guestSessionRepository.GetItemAsync(guestState.GuestSessionId);

            return new CurrentProjectState 
            { 
                Project = project,
                GuestSession = guestSession, 
                LobbyState = lobbyState
            };
        }

        public async Task<bool> DetermineGuestAccessAsync(Guid userId, Guid projectId)
        {
            var guestSessions = await _guestSessionRepository.GetItemsAsync(g => g.UserId == userId && g.ProjectId == projectId);

            var guestSession = guestSessions.FirstOrDefault();

            return guestSession?.GuestSessionState == GuestState.InProject || guestSession?.GuestSessionState == GuestState.PromotedToProjectMember;
        }
        
        // TODO: put the message back
        public async Task<UpdateGuestSessionStateResponse> UpdateGuestSessionStateAsync(UpdateGuestSessionStateRequest request)
        {
            var result = new UpdateGuestSessionStateResponse
            {
                ResultCode = UpdateGuestSessionStateResultCodes.Failed
            };

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
                    return result;
                }

                if (currentGuestSession.GuestSessionState == GuestState.Ended && request.GuestSessionState != GuestState.Ended)
                {
                    result.ResultCode = UpdateGuestSessionStateResultCodes.SessionEnded;
                    return result;
                }

                if (currentGuestSession.GuestSessionState == request.GuestSessionState)
                {
                    result.ResultCode = UpdateGuestSessionStateResultCodes.SameAsCurrent;
                    result.GuestSession = currentGuestSession;
                    return result;
                }

                var projectResponse = await _projectApi.GetProjectByIdAsync(currentGuestSession.ProjectId);

                if (!projectResponse.IsSuccess())
                {
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

                    UpdateProjectLobbyState(project.Id, LobbyState.GuestLimitReached);
                    return result;
                }

                currentGuestSession.GuestSessionState = request.GuestSessionState;

                var guestSession = await _guestSessionController.UpdateGuestSessionAsync(currentGuestSession);

                if (guestSession.GuestSessionState == GuestState.InProject && availableGuestCount == 1)
                {
                    UpdateProjectLobbyState(project.Id, LobbyState.GuestLimitReached);
                }
                else if (currentGuestSession.GuestSessionState == GuestState.InProject && request.GuestSessionState != GuestState.InProject && availableGuestCount == 0)
                {
                    UpdateProjectLobbyState(project.Id, LobbyState.Normal);
                }

                result.GuestSession = guestSession;
                result.ResultCode = UpdateGuestSessionStateResultCodes.Success;
            }
            catch (Exception ex)
            {
                result.ResultCode = UpdateGuestSessionStateResultCodes.Failed;
                _logger.Error($"Error updating Guest Session State for guest session {request.GuestSessionId}", ex);
            }

            return result;
        }

        private void UpdateProjectLobbyState(Guid projectId, LobbyState lobbyStatus)
        {
            var projectLobbyState = new ProjectLobbyState { ProjectId = projectId,  LobbyState = lobbyStatus };
            // TODO - update project lobby
            _eventService.Publish(EventNames.ProjectStatusUpdated, projectLobbyState);
        }

        private async Task<int> GetAvailableGuestCountAsync(Guid projectId)
        {
            var guestSessionsInProject = await _guestSessionRepository.GetItemsAsync(x => x.ProjectId == projectId);


            return 10 - guestSessionsInProject.Count(g => g.GuestSessionState == GuestState.InProject);
        }
    }

    public class KeyResolver
    {
        const string GetGuestUserStatePrefix = "ProjectItemSessionData";

        public static string GetGuestUserStateKey(string sessionId)
        {
            return $"{GetGuestUserStatePrefix}:{sessionId}";
        }
    }

    public class GuestProjectState
    {
        public Guid ProjectId { get; set; }
        public Guid TenantId { get; set; }
        public Guid GuestSessionId { get; set; }
        public string UserSessionId { get; set; }
        public GuestState State { get; set; }
    }

    public class CurrentProjectState
    {
        public Project Project { get; set; }
        public LobbyState LobbyState { get; set; }
        public GuestSession GuestSession { get; set; }
        public bool UserHasAccess { get; set; }
        public string Message { get; set; }
    }

    public class UpdateGuestSessionStateRequest
    {
        public Guid GuestSessionId { get; set; }
        public GuestState GuestSessionState { get; set; }
    }

    public class UpdateGuestSessionStateResponse
    {
        public GuestSession GuestSession { get; set; }
        public UpdateGuestSessionStateResultCodes ResultCode { get; set; }
        public string Message { get; set; }
    }

    public enum UpdateGuestSessionStateResultCodes
    {
        Success,
        SameAsCurrent,
        ProjectFull,
        SessionEnded,
        Failed
    }
}