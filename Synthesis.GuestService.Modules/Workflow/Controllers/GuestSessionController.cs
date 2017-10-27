using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation.Results;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Dao.Models;
using Synthesis.GuestService.Validators;
using Synthesis.GuestService.Workflow.Interfaces;
using Synthesis.GuestService.Workflow.ServiceInterop;
using Synthesis.GuestService.Workflow.ServiceInterop.Responses;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;

namespace Synthesis.GuestService.Workflow.Controllers
{
    /// <summary>
    ///     Represents a controller for GuestSession resources.
    /// </summary>
    /// <seealso cref="Synthesis.GuestService.Workflow.Interfaces.IGuestSessionController" />
    public class GuestSessionController : IGuestSessionController
    {
        private const int MaxGuestsAllowedInProject = 10;
        private readonly IEventService _eventService;
        private readonly IRepository<GuestSession> _guestSessionRepository;
        private readonly ILogger _logger;
        private readonly IParticipantInterop _participantInterop;
        private readonly IProjectInterop _projectInterop;
        private readonly ISettingsInterop _settingsInterop;
        private readonly IUserInterop _userInterop;
        private readonly IValidatorLocator _validatorLocator;

        /// <summary>
        ///     Initializes a new instance of the <see cref="GuestSessionController" /> class.
        /// </summary>
        /// <param name="repositoryFactory">The repository factory.</param>
        /// <param name="validatorLocator">The validator locator.</param>
        /// <param name="eventService">The event service.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="projectInterop"></param>
        /// <param name="settingsInterop"></param>
        /// <param name="userInterop"></param>
        /// <param name="participantInterop"></param>
        public GuestSessionController(
            IRepositoryFactory repositoryFactory,
            IValidatorLocator validatorLocator,
            IEventService eventService,
            ILogger logger,
            IProjectInterop projectInterop,
            ISettingsInterop settingsInterop,
            IUserInterop userInterop,
            IParticipantInterop participantInterop)
        {
            _guestSessionRepository = repositoryFactory.CreateRepository<GuestSession>();

            _validatorLocator = validatorLocator;
            _eventService = eventService;
            _logger = logger;
            _projectInterop = projectInterop;
            _settingsInterop = settingsInterop;
            _userInterop = userInterop;
            _participantInterop = participantInterop;
        }

        public async Task<GuestSession> CreateGuestSessionAsync(GuestSession model)
        {
            var validationResult = _validatorLocator.Validate<GuestSessionValidator>(model);
            if (!validationResult.IsValid)
            {
                _logger.Warning("Validation failed while attempting to create a GuestSession resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            model.Id = model.Id == Guid.Empty ? Guid.NewGuid() : model.Id;
            model.CreatedDateTime = DateTime.UtcNow;
            var result = await _guestSessionRepository.CreateItemAsync(model);

            _eventService.Publish(EventNames.GuestSessionCreated, result);

            return result;
        }

        public async Task<GuestSession> GetGuestSessionAsync(Guid id)
        {
            var validationResult = _validatorLocator.Validate<GuestSessionIdValidator>(id);
            if (!validationResult.IsValid)
            {
                _logger.Warning("Failed to validate the resource id while attempting to retrieve a GuestSession resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var result = await _guestSessionRepository.GetItemAsync(id);
            if (result != null)
            {
                return result;
            }

            _logger.Warning($"A GuestSession resource could not be found for id {id}");
            throw new NotFoundException("GuestSession could not be found");
        }

        public async Task<IEnumerable<GuestSession>> GetGuestSessionsByProjectIdAsync(Guid projectId)
        {
            var validationResult = _validatorLocator.Validate<ProjectIdValidator>(projectId);
            if (!validationResult.IsValid)
            {
                _logger.Warning("Failed to validate the projectId while attempting to retrieve GuestSession resources.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var result = await _guestSessionRepository.GetItemsAsync(x => x.ProjectId == projectId);
            if (result != null)
            {
                return result;
            }

            _logger.Warning($"GuestSession resources could not be found for projectId {projectId}");
            throw new NotFoundException("GuestSessions could not be found");
        }

        public async Task<ProjectStatus> GetProjectStatusAsync(Guid projectId)
        {
            var validationResult = _validatorLocator.Validate<ProjectIdValidator>(projectId);
            if (!validationResult.IsValid)
            {
                _logger.Warning("Failed to validate the projectId while attempting to reset the project access code.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var isGuestLimitReached = await GetProjectActiveGuestSessionCountAsync(projectId) >= MaxGuestsAllowedInProject;
            var isHostPresent = await IsHostCurrentlyPresentInProjectAsync(projectId);

            var lobbyStatus = ProjectStatus.CalculateLobbyStatus(isGuestLimitReached, isHostPresent);
            return new ProjectStatus(lobbyStatus);
        }

        public async Task<GuestSession> UpdateGuestSessionAsync(GuestSession guestSessionModel)
        {
            var errors = GetFailures(Tuple.Create<Type, object>(typeof(GuestSessionIdValidator), guestSessionModel.Id),
                                     Tuple.Create<Type, object>(typeof(GuestSessionValidator), guestSessionModel));
            if (errors.Any())
            {
                _logger.Warning("Failed to validate the resource id and/or resource while attempting to update a GuestSession resource.");
                throw new ValidationFailedException(errors);
            }

            var result = await _guestSessionRepository.UpdateItemAsync(guestSessionModel.Id, guestSessionModel);

            _eventService.Publish(EventNames.GuestSessionUpdated, result);

            return result;
        }

        private List<ValidationFailure> GetFailures(params Tuple<Type, object>[] validations)
        {
            var errors = new List<ValidationFailure>();

            foreach (var v in validations)
            {
                var validator = _validatorLocator.GetValidator(v.Item1);
                var result = validator?.Validate(v.Item2);
                if (result?.IsValid == false)
                {
                    errors.AddRange(result.Errors);
                }
            }

            return errors;
        }

        private async Task<int> GetProjectActiveGuestSessionCountAsync(Guid projectId)
        {
            var projectGuests = await _guestSessionRepository.GetItemsAsync(x => x.ProjectId == projectId);
            return projectGuests.Count(g => g.GuestSessionState == GuestState.InProject);
        }

        private async Task<bool> IsHostCurrentlyPresentInProjectAsync(Guid projectId)
        {
            var project = await _projectInterop.GetProjectByIdAsync(projectId);
            if (project == null)
            {
                _logger.Warning($"Failed to retrieve the project with id {projectId} when verifying if host is present.");
                throw new NotFoundException($"Error retrieving project with id {projectId} while verifying if host is present.");
            }

            var participants = await _participantInterop.GetParticipantsByProjectId(projectId);
            if (participants == null)
            {
                _logger.Warning($"Failed to retrieve the participants for projectId {projectId} when verifying if host is present.");
                throw new NotFoundException($"Error retrieving participants for project {projectId} while verifying if host is present.");
            }

            if (participants.Any())
            {
                return participants.Any(p => p.UserId == project.OwnerId);
            }

            _logger.Warning($"There are no current participants for projectId {projectId} when verifying if host is present.");
            throw new NotFoundException($"There are no participants for project {projectId} while verifying if host is present.");
        }
    }
}