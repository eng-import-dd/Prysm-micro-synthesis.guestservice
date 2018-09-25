using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.Guest.ProjectContext.Models;
using Synthesis.Guest.ProjectContext.Services;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.InternalApi.Enums;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.InternalApi.Requests;
using Synthesis.GuestService.Validators;
using Synthesis.Http.Microservice;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
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

        private readonly IValidatorLocator _validatorLocator;

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
            IUserApi userApi,
            IValidatorLocator validatorLocator)
        {
            _validatorLocator = validatorLocator;

            _guestSessionRepository = repositoryFactory.CreateRepository<GuestSession>();

            _guestSessionController = guestSessionController;
            _projectLobbyStateController = projectLobbyStateController;

            _serviceToServiceProjectApi = serviceToServiceProjectApi;
            _serviceToServiceProjectAccessApi = serviceToServiceProjectAccessApi;
            _userApi = userApi;
            _projectGuestContextService = projectGuestContextService;
        }

        public async Task AddUserToProject(Guid userToAddId, Guid projectId, Guid currentUserId, Guid? currentUserTenantId)
        {
            var validationResult = _validatorLocator.ValidateMany(new Dictionary<Type, object>
            {
                { typeof(UserIdValidator), userToAddId },
                { typeof(ProjectIdValidator), projectId }
            });
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            // Grant membership?
            await _serviceToServiceProjectAccessApi.GrantProjectMembershipAsync(userToAddId, projectId);
            
            // send participant email? - the prod cloud does this

            var guestSession = await GetGuestSessionForUser(userToAddId, projectId);

            var guestSessionRequest = new UpdateGuestSessionStateRequest
            {
                GuestSessionId = guestSession.Id,
                GuestSessionState = InternalApi.Enums.GuestState.PromotedToProjectMember
            };
            var guestSessionStateResponse = await _guestSessionController.UpdateGuestSessionStateAsync(guestSessionRequest, userToAddId);
            if (guestSessionStateResponse.ResultCode == UpdateGuestSessionStateResultCodes.Failed)
            {
                throw new Exception($"Failed to update the guest session state for SessionId={guestSession.Id}. Message={guestSessionStateResponse.Message}");
            }

            //guestSession.GuestSessionState = InternalApi.Enums.GuestState.PromotedToProjectMember;
            //await _guestSessionController.UpdateGuestSessionAsync(guestSession, userToAddId);

            // UpdateGuestSessionState(newState, userProjectDTO.UserId);
            // DeleteGuestSession(guestSession.GuestSessionId);
            // BroadcastGuestListChanged(userProjectDTO.ProjectId);
        }

        private async Task<GuestSession> GetGuestSessionForUser(Guid userId, Guid projectId)
        {
            var guestSessions = await _guestSessionController.GetGuestSessionsByProjectIdForUserAsync(projectId, userId);
            if (!guestSessions.Any())
            {
                throw new Exception($"Failed to get guest session for userId={userId} and projectId={projectId}");
            }

            return guestSessions.FirstOrDefault();
        }
        
        public async Task<CurrentProjectState> SetProjectGuestContextAsync(Guid projectId, string accessCode, Guid currentUserId, Guid? currentUserTenantId)
        {
            if (projectId.Equals(Guid.Empty))
            {
                return await ClearGuestSessionState(currentUserId);
            }

            (ProjectGuestContext guestContext,
             MicroserviceResponse<IEnumerable<Guid>> projectUsersResponse,
             MicroserviceResponse<Project> projectResponse) = await LoadData(projectId);

            if (!projectResponse.IsSuccess() || projectResponse.Payload == null)
            {
                throw new InvalidOperationException($"Error fetching project {projectId} with service to service client, {projectResponse?.ResponseCode} - {projectResponse?.ReasonPhrase}");
            }
            if (!projectUsersResponse.IsSuccess() || projectUsersResponse.Payload == null)
            {
                throw new InvalidOperationException($"Error fetching project users {projectId}, {projectUsersResponse.ResponseCode} - {projectUsersResponse.ReasonPhrase}");
            }

            var project = projectResponse?.Payload;
            var userIsProjectMember = projectUsersResponse.Payload.Any(userId => userId == currentUserId);
            var userHasSameTenant = project.TenantId == currentUserTenantId;
            var userIsProjectMemberInSameTenant = userIsProjectMember & userHasSameTenant;
            var isProjectGuest = guestContext != null && guestContext.IsGuest();

            if (isProjectGuest && userIsProjectMemberInSameTenant)
            {
                //User is in project's account and was a guest who was promoted to a full
                //member, clear guest properties. This changes the return value of ProjectGuestContextService.IsGuestAsync() to false.
                await _projectGuestContextService.SetProjectGuestContextAsync(new ProjectGuestContext());
            }

            var userHasAccess = isProjectGuest ?
                await DetermineGuestAccessAsync(currentUserId, projectId) :
                userIsProjectMemberInSameTenant;

            if (userHasAccess)
            {
                return await CreateCurrentProjectState(project, true);
            }

            if (isProjectGuest && guestContext?.ProjectId == project?.Id)
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

            var guestVerifyResponse = await _guestSessionController.VerifyGuestAsync(verifyRequest, currentUserTenantId);

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

            var grantUserResponse = await _serviceToServiceProjectAccessApi.GrantProjectMembershipAsync(currentUserId, project.Id);
            if (!grantUserResponse.IsSuccess())
            {
                throw new InvalidOperationException("Failed to add user to project");
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

            await _projectGuestContextService.SetProjectGuestContextAsync(new ProjectGuestContext{ GuestState = GuestState.Ended });

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

        private async Task<bool> DetermineGuestAccessAsync(Guid userId, Guid projectId)
        {
            var guestSessions = await _guestSessionRepository.GetItemsAsync(g => g.UserId == userId && g.ProjectId == projectId);

            var guestSession = guestSessions.OrderByDescending(x => x.CreatedDateTime).FirstOrDefault();

            return guestSession?.GuestSessionState == InternalApi.Enums.GuestState.InProject || guestSession?.GuestSessionState == InternalApi.Enums.GuestState.PromotedToProjectMember;
        }

        private async Task<(
                ProjectGuestContext guestContext,
                MicroserviceResponse<IEnumerable<Guid>> projectUsersResponse,
                MicroserviceResponse<Project> projectResponse)>
            LoadData(Guid projectId)
        {
            var guestContextTask =  _projectGuestContextService.GetProjectGuestContextAsync();
            var projectUsersTask =  _serviceToServiceProjectAccessApi.GetProjectMemberUserIdsAsync(projectId, MemberRoleFilter.FullUser);
            var projectTask = _serviceToServiceProjectApi.GetProjectByIdAsync(projectId);

            await Task.WhenAll(guestContextTask, projectUsersTask, projectTask);

            var guestContext = await guestContextTask;
            var projectResponse = await projectTask;
            var projectUsersResponse = await projectUsersTask;

            return (guestContext, projectUsersResponse, projectResponse);
        }
    }
}