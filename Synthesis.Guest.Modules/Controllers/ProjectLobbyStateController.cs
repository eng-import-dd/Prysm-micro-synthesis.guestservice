using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.GuestService.InternalApi.Constants;
using Synthesis.GuestService.InternalApi.Enums;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.Validators;
using Synthesis.Http.Microservice;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.ParticipantService.InternalApi.Constants;
using Synthesis.ParticipantService.InternalApi.Models;
using Synthesis.ParticipantService.InternalApi.Services;
using Synthesis.ProjectService.InternalApi.Api;

namespace Synthesis.GuestService.Controllers
{
    public class ProjectLobbyStateController : IProjectLobbyStateController
    {
        private readonly IRepository<ProjectLobbyState> _projectLobbyStateRepository;
        private readonly IRepository<GuestSession> _guestSessionRepository;
        private readonly IEventService _eventService;
        private readonly IValidatorLocator _validatorLocator;
        private readonly ISessionService _sessionService;
        private readonly IProjectApi _projectApi;
        private readonly ILogger _logger;
        private readonly int _maxGuestsAllowedInProject;

        public ProjectLobbyStateController(IRepositoryFactory repositoryFactory,
            IValidatorLocator validatorLocator,
            ISessionService sessionService,
            IProjectApi projectApi,
            IEventService eventService,
            ILoggerFactory loggerFactory,
            int maxGuestsAllowedInProject)
        {
            _validatorLocator = validatorLocator;
            _sessionService = sessionService;
            _projectApi = projectApi;
            _projectLobbyStateRepository = repositoryFactory.CreateRepository<ProjectLobbyState>();
            _guestSessionRepository = repositoryFactory.CreateRepository<GuestSession>();
            _eventService = eventService;
            _logger = loggerFactory.GetLogger(this);
            _maxGuestsAllowedInProject = maxGuestsAllowedInProject;
        }

        /// <inheritdoc />
        public async Task CreateProjectLobbyStateAsync(Guid projectId)
        {
            var validationResult = _validatorLocator.Validate<ProjectIdValidator>(projectId);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            await _projectLobbyStateRepository.CreateItemAsync(new ProjectLobbyState
            {
                ProjectId = projectId,
                LobbyState = LobbyState.Normal
            });
        }

        /// <inheritdoc />
        public async Task RecalculateProjectLobbyStateAsync(Guid projectId)
        {
            var validationResult = _validatorLocator.Validate<ProjectIdValidator>(projectId);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            var participantTask = _sessionService.GetParticipantsByGroupNameAsync($"{LegacyGroupPrefixes.Project}{projectId}");
            var projectTask = _projectApi.GetProjectByIdAsync(projectId);
            var projectGuestsTask = _guestSessionRepository.GetItemsAsync(x => x.ProjectId == projectId);

            await Task.WhenAll(projectTask, projectGuestsTask);

            IEnumerable<Participant> participantResult;
            try
            {
                participantResult = await participantTask;
            }
            catch (Exception ex)
            {
                await SetProjectLobbyStateToError(projectId);
                _logger.Error($"Failed to retrieve participants for project: {projectId}. Message: {ex.Message}", ex);
                return;
            }

            var projectResult = await projectTask;
            var projectGuestsResult = await projectGuestsTask;

            if (!projectResult.IsSuccess())
            {
                await SetProjectLobbyStateToError(projectId);
                _logger.Error($"Failed to retrieve project: {projectId}. Message: {projectResult.ReasonPhrase}");
                return;
            }

            var project = projectResult.Payload;
            var participants = participantResult?.ToList();

            var isHostPresent = participants?.Any(p => p.UserId == project?.OwnerId) ?? false;
            var isGuestLimitReached = projectGuestsResult.Count(g => g.GuestSessionState == GuestState.InProject) >= _maxGuestsAllowedInProject;

            try
            {
                await _projectLobbyStateRepository.UpdateItemAsync(projectId, new ProjectLobbyState
                {
                    ProjectId = projectId,
                    LobbyState = CalculateLobbyState(isGuestLimitReached, isHostPresent)
                });
            }
            catch (DocumentNotFoundException e)
            {
                throw new NotFoundException($"{nameof(ProjectLobbyState)} for {projectId} was not found.", e);
            }
        }

        public async Task<ProjectLobbyState> UpsertProjectLobbyStateAsync(Guid projectId, ProjectLobbyState projectLobbyState)
        {
            ProjectLobbyState updatedProjectLobbyState;
            try
            {
                updatedProjectLobbyState = await _projectLobbyStateRepository.UpdateItemAsync(projectId, projectLobbyState);
            }
            catch (DocumentNotFoundException)
            {
                updatedProjectLobbyState = await _projectLobbyStateRepository.CreateItemAsync(projectLobbyState);
            }

            _eventService.Publish(EventNames.ProjectStatusUpdated, updatedProjectLobbyState);

            return updatedProjectLobbyState;
        }

        public static LobbyState CalculateLobbyState(bool isGuestLimitReached, bool isHostPresent)
        {
            LobbyState status;

            if (!isHostPresent)
            {
                status = LobbyState.HostNotPresent;
            }
            else if (!isGuestLimitReached)
            {
                status = LobbyState.Normal;
            }
            else
            {
                status = LobbyState.GuestLimitReached;
            }

            return status;
        }

        /// <inheritdoc />
        public async Task<ProjectLobbyState> GetProjectLobbyStateAsync(Guid projectId)
        {
            var validationResult = _validatorLocator.Validate<ProjectIdValidator>(projectId);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            var result = await _projectLobbyStateRepository.GetItemAsync(projectId);
            if (result == null)
            {
                throw new NotFoundException($"ProjectLobbyState with ProjectId {projectId} not found.");
            }

            return result;
        }

        /// <inheritdoc />
        public async Task DeleteProjectLobbyStateAsync(Guid projectId)
        {
            try
            {
                await _projectLobbyStateRepository.DeleteItemAsync(projectId);
            }
            catch (DocumentNotFoundException)
            {
            }
        }

        private async Task SetProjectLobbyStateToError(Guid projectId)
        {
            await _projectLobbyStateRepository.UpdateItemAsync(projectId, new ProjectLobbyState
            {
                ProjectId = projectId,
                LobbyState = LobbyState.Error
            });
        }
    }
}
