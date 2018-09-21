using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using log4net.Repository.Hierarchy;
using Synthesis.DocumentStorage;
using Synthesis.Guest.ProjectContext.Enums;
using Synthesis.Guest.ProjectContext.Models;
using Synthesis.Guest.ProjectContext.Services;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.InternalApi.Enums;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.InternalApi.Requests;
using Synthesis.Http.Microservice;
using Synthesis.Http.Microservice.Constants;
using Synthesis.Nancy.MicroService;
using Synthesis.PrincipalService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Enumerations;
using Synthesis.ProjectService.InternalApi.Models;
using GuestState = Synthesis.Guest.ProjectContext.Enums.GuestState;

namespace Synthesis.GuestService.Controllers
{
    public class ProjectGuestContextController : IProjectGuestContextController
    {
        private readonly IRepository<GuestSession> _guestSessionRepository;

        private readonly IProjectApi _serviceToServiceProjectApi;
        private readonly IProjectGuestContextService _projectGuestContextService;
        private readonly IGuestSessionController _guestSessionController;
        private readonly IProjectLobbyStateController _projectLobbyStateController;
        private readonly IProjectAccessApi _serviceToServiceProjectAccessApi;
        private readonly IUserApi _userApi;

        /// <summary>
        ///     Initializes a new instance of the <see cref="GuestSessionController" /> class.
        /// </summary>
        public ProjectGuestContextController(
            IRepositoryFactory repositoryFactory,
            IGuestSessionController guestSessionController,
            IProjectLobbyStateController projectLobbyStateController,
            IProjectGuestContextService projectGuestContextService,
            IProjectAccessApi serviceToServiceProjectAccessApi,
            IProjectApi serviceToServiceProjectApi,
            IUserApi userApi)
        {
            _guestSessionRepository = repositoryFactory.CreateRepository<GuestSession>();

            _guestSessionController = guestSessionController;
            _projectLobbyStateController = projectLobbyStateController;

            _serviceToServiceProjectApi = serviceToServiceProjectApi;
            _serviceToServiceProjectAccessApi = serviceToServiceProjectAccessApi;
            _userApi = userApi;
            _projectGuestContextService = projectGuestContextService;
        }

        /// <summary>
        /// Assigns the guest
        /// </summary>
        /// <param name="projectId"></param>
        /// <param name="accessCode"></param>
        /// <param name="currentUserId"></param>
        /// <param name="currentUserTenantId"></param>
        /// <returns></returns>
        public async Task<CurrentProjectState> SetProjectGuestContextAsync(Guid projectId, string accessCode, Guid currentUserId, Guid? currentUserTenantId)
        {
            if (projectId.Equals(Guid.Empty))
            {
                return await ClearGuestSessionState(currentUserId);
            }

            var projectResponse = await _serviceToServiceProjectApi.GetProjectByIdAsync(projectId);
            if (!projectResponse.IsSuccess() || projectResponse.Payload == null)
            {
                throw new InvalidOperationException($"Error fetching project {projectId} with service to service client, {projectResponse?.ResponseCode} - {projectResponse?.ReasonPhrase}");
            }

            var projectTenantId = projectResponse.Payload.TenantId;

            (ProjectGuestContext guestContext,
             MicroserviceResponse<IEnumerable<Guid>> projectUsersResponse) = await LoadDataAsync(projectId, projectTenantId);


            if (!projectUsersResponse.IsSuccess() || projectUsersResponse.Payload == null)
            {
                throw new InvalidOperationException($"Error fetching project users {projectId}, {projectUsersResponse.ResponseCode} - {projectUsersResponse.ReasonPhrase}");
            }

            var project = projectResponse?.Payload;
            var userIsFullProjectMember = projectUsersResponse.Payload.Any(userId => userId == currentUserId);

            if (UserHasActiveProjectGuestContext(guestContext) && userIsFullProjectMember)
            {
                //User is in project's account and was a guest who was promoted to a full
                //member, clear guest properties. This changes the return value of ProjectGuestContextService.IsGuestAsync() to false.
                guestContext = new ProjectGuestContext();
                await _projectGuestContextService.SetProjectGuestContextAsync(guestContext);
            }

            var userHasAccess = UserHasActiveProjectGuestContext(guestContext) ?
                await IsGuestCurrentlyAdmittedToProjectAsync(guestContext.GuestSessionId) :
                userIsFullProjectMember;

            if (userHasAccess)
            {
                return await CreateCurrentProjectState(project, true);
            }

            if (UserHasActiveProjectGuestContext(guestContext) && guestContext?.ProjectId == project?.Id)
            {
                return await CreateCurrentProjectState(project, false);
            }

            var userResponse = await _userApi.GetUserAsync(currentUserId);
            if (!userResponse.IsSuccess() || userResponse?.Payload == null)
            {
                throw new InvalidOperationException($"Error fetching user for {currentUserId}, {userResponse?.ResponseCode} - {userResponse?.ReasonPhrase}");
            }

            var userName = !string.IsNullOrEmpty(userResponse.Payload.Email) ? userResponse.Payload.Email : userResponse.Payload.Username;
            var verifyRequest = new GuestVerificationRequest() { Username = userName, ProjectAccessCode = accessCode, ProjectId = projectId };

            var guestVerifyResponse = await _guestSessionController.VerifyGuestAsync(verifyRequest, project, currentUserTenantId);

            if (guestVerifyResponse.ResultCode != VerifyGuestResponseCode.Success)
            {
                throw new InvalidOperationException($"Failed to verify guest for User.Id = {currentUserId}, Project.Id = {projectId}. ResponseCode = {guestVerifyResponse.ResultCode}. Reason = {guestVerifyResponse.Message}");
            }

            var newSession = await _guestSessionController.CreateGuestSessionAsync(new GuestSession
            {
                UserId = currentUserId,
                ProjectId = projectId,
                ProjectAccessCode = project.GuestAccessCode,
                GuestSessionState = InternalApi.Enums.GuestState.InLobby
            });

            await _projectGuestContextService.SetProjectGuestContextAsync(new ProjectGuestContext()
            {
                GuestSessionId = newSession.Id,
                ProjectId = project.Id,
                GuestState = Guest.ProjectContext.Enums.GuestState.InLobby,
                TenantId = project.TenantId
            });


            if (!userIsFullProjectMember)
            {
                // Add the user as a guest member to the project.
                // WARNING: Order of operations is significant here. Project membership call must be made only after the 
                // the guest session has been successfully created. Otherwise the user may be given the wrong kind of membership.
                var grantUserResponse = await _serviceToServiceProjectAccessApi.GrantProjectMembershipAsync(currentUserId, project.Id, new List<KeyValuePair<string, string>>() { HeaderKeys.CreateTenantHeaderKey(projectTenantId) });
                if (!grantUserResponse.IsSuccess())
                {
                    throw new InvalidOperationException("Failed to add user to project");
                }
            }

            return await CreateCurrentProjectState(project, false);
        }

        private async Task<CurrentProjectState> ClearGuestSessionState(Guid principalId)
        {
            var guestContext = await _projectGuestContextService.GetProjectGuestContextAsync();

            if (guestContext == null || guestContext.GuestSessionId == Guid.Empty)
            {
                return new CurrentProjectState()
                {
                    Message = "No guest session from which to clear the current project."
                };
            }

            var guestSessionRequest = new UpdateGuestSessionStateRequest()
            {
                GuestSessionId = guestContext.GuestSessionId,
                GuestSessionState = InternalApi.Enums.GuestState.Ended
            };

            var guestSessionStateResponse = await _guestSessionController.UpdateGuestSessionStateAsync(guestSessionRequest, principalId);

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

            try
            {
                var lobbyStateResponse = await _projectLobbyStateController.GetProjectLobbyStateAsync(project.Id);
                lobbyState = lobbyStateResponse.LobbyState;
            }
            catch (NotFoundException)
            {
                throw new NotFoundException(ResponseReasons.NotFoundProject);
            }

            var guestContext = await _projectGuestContextService.GetProjectGuestContextAsync();

            var guestSession = guestContext == null || guestContext.GuestSessionId == Guid.Empty
                ? null
                : await _guestSessionRepository.GetItemAsync(guestContext.GuestSessionId);

            return new CurrentProjectState
            {
                Project = project,
                GuestSession = guestSession,
                UserHasAccess = userHasAccess,
                LobbyState = lobbyState,
                Message = "Successfully set current project"
            };
        }

        private async Task<bool> IsGuestCurrentlyAdmittedToProjectAsync(Guid guestSessionId)
        {
            var guestSession = await _guestSessionRepository.GetItemAsync(guestSessionId);

            return guestSession?.GuestSessionState == InternalApi.Enums.GuestState.InProject || guestSession?.GuestSessionState == InternalApi.Enums.GuestState.PromotedToProjectMember;
        }

        private bool UserHasActiveProjectGuestContext(ProjectGuestContext context)
        {
            return context != null && context.ProjectId != Guid.Empty && context.TenantId != Guid.Empty
                && context.GuestSessionId != Guid.Empty && context.GuestState != GuestState.Ended;
        }

        private async Task<(
                ProjectGuestContext guestContext,
                MicroserviceResponse<IEnumerable<Guid>> projectUsersResponse)>
            LoadDataAsync(Guid projectId, Guid projectTenantId)
        {
            var guestContextTask =  _projectGuestContextService.GetProjectGuestContextAsync();
            var projectUsersTask =  _serviceToServiceProjectAccessApi.GetProjectMemberUserIdsAsync(projectId, MemberRoleFilter.FullUser, new List<KeyValuePair<string, string>>(){HeaderKeys.CreateTenantHeaderKey(projectTenantId)});

            await Task.WhenAll(guestContextTask, projectUsersTask);

            var guestContext = await guestContextTask;
            var projectUsersResponse = await projectUsersTask;

            return (guestContext, projectUsersResponse);
        }
    }
}