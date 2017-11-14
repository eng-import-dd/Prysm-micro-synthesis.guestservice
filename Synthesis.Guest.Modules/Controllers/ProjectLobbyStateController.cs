using Synthesis.DocumentStorage;
using Synthesis.GuestService.ApiWrappers.Interfaces;
using Synthesis.GuestService.Enums;
using Synthesis.GuestService.Models;
using Synthesis.GuestService.Validators;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Synthesis.Configuration;
using Synthesis.GuestService.Extensions;

namespace Synthesis.GuestService.Controllers
{
    public class ProjectLobbyStateController : IProjectLobbyStateController
    {
        private readonly IRepository<ProjectLobbyState> _projectLobbyStateRepository;
        private readonly IRepository<GuestSession> _guestSessionRepository;
        private readonly IValidatorLocator _validatorLocator;
        private readonly IParticipantApiWrapper _participantApi;
        private readonly IProjectApiWrapper _projectApi;
        private readonly int _maxGuestsAllowedInProject;

        public ProjectLobbyStateController(IRepositoryFactory repositoryFactory, 
            IValidatorLocator validatorLocator, 
            IParticipantApiWrapper participantApi,
            IProjectApiWrapper projectApi,
            IAppSettingsReader appSettingsReader,
            int maxGuestsAllowedInProject)
        {
            _validatorLocator = validatorLocator;
            _participantApi = participantApi;
            _projectApi = projectApi;
            _projectLobbyStateRepository = repositoryFactory.CreateRepository<ProjectLobbyState>();
            _guestSessionRepository = repositoryFactory.CreateRepository<GuestSession>();
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

            var participantResult = participantTask.Result;
            var projectResult = projectTask.Result;
            var projectGuestsResult = projectGuestsTask.Result;

            if (projectResult.ResponseCode == HttpStatusCode.NotFound)
            {
                throw new NotFoundException($"Project {projectId} does not exist");
            }

            projectResult.VerifySuccess($"Failed to retrieve project {projectId} when verifying if host is present.");
            participantResult.VerifySuccess($"Failed to retrieve participants for project {projectId} when verifying if host is present.");

            var project = projectResult.Payload;
            var participants = participantResult.Payload.ToList();

            var isHostPresent = participants.Any(p => p.UserId == project.OwnerId);
            var isGuestLimitReached = projectGuestsResult.Count(g => g.GuestSessionState == GuestState.InProject) >= _maxGuestsAllowedInProject;

            await _projectLobbyStateRepository.UpdateItemAsync(projectId, new ProjectLobbyState
            {
                ProjectId = projectId,
                LobbyState = ProjectLobbyState.CalculateLobbyState(isGuestLimitReached, isHostPresent)
            });
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
    }
}
