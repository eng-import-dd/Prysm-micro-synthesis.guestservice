using System;
using System.Linq;
using System.Threading.Tasks;
using Synthesis.DocumentStorage;
using Synthesis.GuestService.ApiWrappers.Interfaces;
using Synthesis.GuestService.Enums;
using Synthesis.GuestService.Extensions;
using Synthesis.GuestService.Models;
using Synthesis.GuestService.Validators;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;

namespace Synthesis.GuestService.Controllers
{
    public class ProjectLobbyStateController : IProjectLobbyStateController
    {
        private readonly IRepository<ProjectLobbyState> _projectLobbyStateRepository;
        private readonly IRepository<GuestSession> _guestSessionRepository;
        private readonly IValidatorLocator _validatorLocator;
        private readonly IParticipantApiWrapper _participantApi;
        private readonly IProjectApiWrapper _projectApi;
        private readonly ILogger _logger;
        private readonly int _maxGuestsAllowedInProject;

        public ProjectLobbyStateController(IRepositoryFactory repositoryFactory, 
            IValidatorLocator validatorLocator, 
            IParticipantApiWrapper participantApi,
            IProjectApiWrapper projectApi,
            ILoggerFactory loggerFactory,
            int maxGuestsAllowedInProject)
        {
            _validatorLocator = validatorLocator;
            _participantApi = participantApi;
            _projectApi = projectApi;
            _projectLobbyStateRepository = repositoryFactory.CreateRepository<ProjectLobbyState>();
            _guestSessionRepository = repositoryFactory.CreateRepository<GuestSession>();
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
                LobbyState = LobbyState.Undefined
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

            var participantTask = _participantApi.GetParticipantsByProjectIdAsync(projectId);
            var projectTask = _projectApi.GetProjectByIdAsync(projectId);
            var projectGuestsTask = _guestSessionRepository.GetItemsAsync(x => x.ProjectId == projectId);

            await Task.WhenAll(participantTask, projectTask, projectGuestsTask);

            var participantResult = await participantTask;
            var projectResult = await projectTask;
            var projectGuestsResult = await projectGuestsTask;

            if (!projectResult.IsSuccess())
            {
                await SetProjectLobbyStateToError(projectId);
                _logger.Error($"Failed to retrieve project: {projectId}. Message: {projectResult.ReasonPhrase}");
                return;
            }

            if(!participantResult.IsSuccess())
            {
                await SetProjectLobbyStateToError(projectId);
                _logger.Error($"Failed to retrieve participants for project: {projectId}. Message: {participantResult.ReasonPhrase}");
                return;
            }

            var project = projectResult.Payload;
            var participants = participantResult?.Payload?.ToList();

            var isHostPresent = participants?.Any(p => p.UserId == project?.OwnerId) ?? false;
            var isGuestLimitReached = projectGuestsResult.Count(g => g.GuestSessionState == GuestState.InProject) >= _maxGuestsAllowedInProject;

            try
            {
                await _projectLobbyStateRepository.UpdateItemAsync(projectId, new ProjectLobbyState
                {
                    ProjectId = projectId,
                    LobbyState = ProjectLobbyState.CalculateLobbyState(isGuestLimitReached, isHostPresent)
                });
            }
            catch (DocumentNotFoundException e)
            {
                throw new NotFoundException($"{nameof(ProjectLobbyState)} for {projectId} was not found.", e);
            }
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
