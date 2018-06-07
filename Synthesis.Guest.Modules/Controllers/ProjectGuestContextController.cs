using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Synthesis.DocumentStorage;
using Synthesis.GuestService.InternalApi.Enums;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.InternalApi.Services;
using Synthesis.Http.Microservice;
using Synthesis.PrincipalService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Models;

namespace Synthesis.GuestService.Controllers
{
    public class ProjectGuestContextController : IProjectGuestContextController
    {
        private readonly IRepository<GuestSession> _guestSessionRepository;
        private readonly IProjectApi _projectApi;

        private readonly IProjectApi _serviceToServiceProjectApi;
        private readonly IProjectGuestContextService _projectGuestContextService;
        private readonly IGuestSessionController _guestSessionController;
        private readonly IProjectLobbyStateController _projectLobbyStateController;
        private readonly IProjectAccessApi _projectAccessApi;
        private readonly IUserApi _userApi;

        /// <summary>
        ///     Initializes a new instance of the <see cref="GuestSessionController" /> class.
        /// </summary>
        public ProjectGuestContextController(
            IRepositoryFactory repositoryFactory,
            IGuestSessionController guestSessionController,
            IProjectLobbyStateController projectLobbyStateController,
            IProjectGuestContextService projectGuestContextService,
            IProjectAccessApi projectAccessApi,
            IProjectApi projectApi,
            IProjectApi serviceToServiceProjectApi,
            IUserApi userApi)
        {
            _guestSessionRepository = repositoryFactory.CreateRepository<GuestSession>();

            _guestSessionController = guestSessionController;
            _projectLobbyStateController = projectLobbyStateController;

            _projectApi = projectApi;
            _serviceToServiceProjectApi = serviceToServiceProjectApi;
            _projectAccessApi = projectAccessApi;
            _userApi = userApi;
            _projectGuestContextService = projectGuestContextService;
        }

        /// <summary>
        /// Assigns the guest
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="accessCode"></param>
        /// <param name="currentUserId"></param>
        /// <returns></returns>
        public async Task<CurrentProjectState> SetProjectGuestContextAsync(Guid projectId, string accessCode, Guid currentUserId)
        {
            if (projectId.Equals(Guid.Empty))
            {
                var projectState = await ClearGuestSessionState();
                return projectState;
                //return await ClearGuestSessionState();
            }

            (ProjectGuestContext guestProjectState,
             MicroserviceResponse<IEnumerable<Guid>> projectUsersResponse,
             MicroserviceResponse<Project> projectResponse) = await LoadData(projectId);

            if ((!projectResponse.IsSuccess() && projectResponse.ResponseCode != HttpStatusCode.Forbidden) || projectResponse.Payload == null)
            {
                throw new InvalidOperationException($"Error fetching project {projectId}, {projectResponse.ResponseCode} - {projectResponse.ReasonPhrase}");
            }
            if (!projectUsersResponse.IsSuccess() || projectUsersResponse.Payload == null)
            {
                throw new InvalidOperationException($"Error fetching project users {projectId}, {projectUsersResponse.ResponseCode} - {projectUsersResponse.ReasonPhrase}");
            }

            var project = projectResponse.Payload;
            var userIsProjectMember = projectUsersResponse.Payload.Any(userId => userId == currentUserId);
            var isGuest = await _projectGuestContextService.IsGuestAsync();

            if (isGuest && userIsProjectMember)
            {
                //User is in project's account and was a guest who was promoted to a full
                //member, clear guest properties. This changes the return value of ProjectGuestContextService.IsGuestAsync() to false.
                await _projectGuestContextService.SetProjectGuestContextAsync(new ProjectGuestContext());
            }

            var userHasAccess = isGuest ?
                await DetermineGuestAccessAsync(currentUserId, projectId) :
                projectResponse.ResponseCode != HttpStatusCode.Forbidden;

            if (projectResponse.ResponseCode != HttpStatusCode.Forbidden)
            {
                return await CreateCurrentProjectState(project, userHasAccess);
            }

            // The user does not have access to the project so we need to fetch it with a service to service client
            var serviceToServiceProjectResponse = await _serviceToServiceProjectApi.GetProjectByIdAsync(projectId);
            if (!serviceToServiceProjectResponse.IsSuccess() || serviceToServiceProjectResponse?.Payload == null)
            {
                throw new InvalidOperationException($"Error fetching project {projectId} with service to service client, {serviceToServiceProjectResponse?.ResponseCode} - {serviceToServiceProjectResponse?.ReasonPhrase}");
            }

            project = serviceToServiceProjectResponse.Payload;

            if (isGuest && guestProjectState.ProjectId == project.Id)
            {
                return await CreateCurrentProjectState(project, userHasAccess);
            }

            var userResponse = await _userApi.GetUserAsync(currentUserId);
            if (!userResponse.IsSuccess() || userResponse?.Payload == null)
            {
                throw new InvalidOperationException($"Error fetching user for {currentUserId}, {userResponse?.ResponseCode} - {userResponse?.ReasonPhrase}");
            }

            var guestVerifyResponse = await _guestSessionController.VerifyGuestAsync(userResponse.Payload.Username, projectId, accessCode);

            if (guestVerifyResponse.ResultCode != VerifyGuestResponseCode.Success)
            {
                throw new InvalidOperationException($"Failed to verify guest for User.Id = {currentUserId}, Project.Id = {projectId}. ");
            }

            var newSession = await _guestSessionController.CreateGuestSessionAsync(new GuestSession
            {
                UserId = currentUserId,
                ProjectId = projectId,
                ProjectAccessCode = project.GuestAccessCode,
                GuestSessionState = GuestState.InLobby
            });

            await _projectGuestContextService.SetProjectGuestContextAsync(new ProjectGuestContext()
            {
                GuestSessionId = newSession.Id, ProjectId = project.Id, GuestState = GuestState.InLobby
            });

            //TODO: CU-598 - This is where a second call to DetermineGuestAccess occurred in the monolith - and it seems both there
            //                and here and that it should be false because GuestSessionState == GuestState.InLobby. Therefore it
            //                seems that the second arg of CreateCurrentProjectState should just be false rather than userHasAccess.
            //                This is because userHasAccess was calculated much earlier and the state of the user's guest session is 
            //                now different.
            return await CreateCurrentProjectState(project, userHasAccess);
        }

        private async Task<CurrentProjectState> ClearGuestSessionState()
        {
            var guestUserState = await _projectGuestContextService.GetProjectGuestContextAsync();

            if (guestUserState == null || guestUserState.GuestSessionId == Guid.Empty)
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

            var guestSessionStateResponse = await _guestSessionController.UpdateGuestSessionStateAsync(guestSessionRequest);

            await _projectGuestContextService.SetProjectGuestContextAsync(new ProjectGuestContext());

            if (guestSessionStateResponse.ResultCode == UpdateGuestSessionStateResultCodes.Success)
            {
                return new CurrentProjectState()
                {
                    Message = "Cleared current project and ended guest session."
                };
            }

            throw new InvalidOperationException($"Could not clear the current project or end the guest session. {guestSessionStateResponse?.Message}");
        }

        private async Task<CurrentProjectState> CreateCurrentProjectState(Project project, bool userHasAccess)
        {
            LobbyState lobbyState;

            var lobbyStateTask = _projectLobbyStateController.GetProjectLobbyStateAsync(project.Id);
            var guestState = await _projectGuestContextService.GetProjectGuestContextAsync();

            try
            {
                var lobbyStateResponse = await lobbyStateTask;
                lobbyState = lobbyStateResponse.LobbyState;
            }
            catch (Nancy.MicroService.NotFoundException)
            {
                lobbyState = LobbyState.Normal;
            }

            var guestSession = guestState == null || guestState.GuestSessionId == Guid.Empty
                ? null
                : await _guestSessionRepository.GetItemAsync(guestState.GuestSessionId);

            return new CurrentProjectState
            {
                Project = project,
                GuestSession = guestSession,
                UserHasAccess = userHasAccess,
                LobbyState = lobbyState
            };
        }

        private async Task<bool> DetermineGuestAccessAsync(Guid userId, Guid projectId)
        {
            var guestSessions = await _guestSessionRepository.GetItemsAsync(g => g.UserId == userId && g.ProjectId == projectId);

            var guestSession = guestSessions.FirstOrDefault();

            return guestSession?.GuestSessionState == GuestState.InProject || guestSession?.GuestSessionState == GuestState.PromotedToProjectMember;
        }

        private async Task<(
                ProjectGuestContext guestProjectState,
                MicroserviceResponse<IEnumerable<Guid>> projectUsersResponse,
                MicroserviceResponse<Project> projectResponse)>
            LoadData(Guid projectId)
        {
            var guestUserTask =  _projectGuestContextService.GetProjectGuestContextAsync();
            var projectUsersTask =  _projectAccessApi.GetUserIdsByProjectAsync(projectId);
            var projectTask =  _projectApi.GetProjectByIdAsync(projectId);

            await Task.WhenAll(guestUserTask, projectUsersTask, projectTask);

            var guestProjectState = await guestUserTask;
            var projectResponse = await _projectApi.GetProjectByIdAsync(projectId);
            var projectUsersResponse = await projectUsersTask;

            return (guestProjectState, projectUsersResponse, projectResponse);
        }
    }
}