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
using Synthesis.Logging;
using Synthesis.PrincipalService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Models;

namespace Synthesis.GuestService.Controllers
{
    public class ProjectGuestContextController : IProjectGuestContextController
    {
        private readonly IRepository<GuestSession> _guestSessionRepository;
        private readonly IProjectApi _projectApi;
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
            IUserApi userApi)
        {
            _guestSessionRepository = repositoryFactory.CreateRepository<GuestSession>();

            _guestSessionController = guestSessionController;
            _projectLobbyStateController = projectLobbyStateController;

            _projectApi = projectApi;
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
                return await ClearGuestSessionState();
            }

            (ProjectGuestContext guestProjectState,
             MicroserviceResponse<IEnumerable<Guid>> projectUsersResponse,
             MicroserviceResponse<Project> projectResponse) = await LoadData(projectId);

            if (!projectResponse.IsSuccess() && projectResponse.ResponseCode != HttpStatusCode.Forbidden)
            {
                throw new InvalidOperationException($"Error fetching project {projectId}, {projectResponse.ResponseCode} - {projectResponse.ReasonPhrase}");
            }
            if (!projectUsersResponse.IsSuccess())
            {
                throw new InvalidOperationException($"Error fetching project users {projectId}, {projectUsersResponse.ResponseCode} - {projectUsersResponse.ReasonPhrase}");
            }

            var project = projectResponse.Payload;
            var userIsProjectMember = projectUsersResponse.Payload.Any(userId => userId == currentUserId);
            var isGuest = await _projectGuestContextService.IsGuestAsync();  // TODO: Move this to an extension method

            if (isGuest && userIsProjectMember)
            {
                //User is in project's account and was a guest who was promoted to a full
                //member, clear guest properties. This changes the value of IsGuest.
                await _projectGuestContextService.SetProjectGuestContextAsync(new ProjectGuestContext());
            }

            var userHasAccess = isGuest ?
                await DetermineGuestAccessAsync(currentUserId, projectId) :
                projectResponse.ResponseCode != HttpStatusCode.Forbidden;

            if (projectResponse.ResponseCode != HttpStatusCode.Forbidden)
            {
                return await CreateCurrentProjectState(project, userHasAccess);
            }

            if (isGuest && guestProjectState.ProjectId == project.Id)
            {
                return await CreateCurrentProjectState(project, userHasAccess);
            }

            var userResonse = await _userApi.GetUserAsync(currentUserId);
            if (!userResonse.IsSuccess())
            {
                throw new InvalidOperationException($"Error fetching user for  {currentUserId}, {userResonse.ResponseCode} - {userResonse.ReasonPhrase}");
            }

            var guestVerifyResponse = await _guestSessionController.VerifyGuestAsync(userResonse.Payload.Username, project.Id, accessCode);

            if (guestVerifyResponse.ResultCode != VerifyGuestResponseCode.Success)
            {
                throw new InvalidOperationException($"Failed to verify guest for {currentUserId} - {guestVerifyResponse}");
            }

            var newSession = await _guestSessionController.CreateGuestSessionAsync(new GuestSession
            {
                UserId = currentUserId,
                ProjectId = project.Id,
                ProjectAccessCode = project.GuestAccessCode,
                GuestSessionState = GuestState.InLobby
            });

            await _projectGuestContextService.SetProjectGuestContextAsync(new ProjectGuestContext()
            {
                GuestSessionId = newSession.Id, ProjectId = project.Id, GuestState = GuestState.InLobby
            });

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

            throw new InvalidOperationException($"Could not clear the current project or end the guest session. {guestSessionStateResponse.Message}");
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