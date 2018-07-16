using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Synthesis.Cache;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.InternalApi.Constants;
using Synthesis.GuestService.InternalApi.Enums;
using Synthesis.GuestService.InternalApi.Models;
using LobbyStateKeyResolver = Synthesis.GuestService.InternalApi.Services.KeyResolver;
using Synthesis.GuestService.Validators;
using Synthesis.Http.Microservice;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.ParticipantService.InternalApi.Constants;
using Synthesis.ParticipantService.InternalApi.Models;
using Synthesis.ParticipantService.InternalApi.Services;
using Synthesis.ProjectService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Models;
using Synthesis.Threading.Tasks;

namespace Synthesis.GuestService.Controllers
{
    public class ProjectLobbyStateController : IProjectLobbyStateController
    {
        private readonly IRepository<ProjectLobbyState> _projectLobbyStateRepository;
        private readonly ICacheAsync _cache;
        private readonly IRepository<GuestSession> _guestSessionRepository;
        private readonly IEventService _eventService;
        private readonly IValidatorLocator _validatorLocator;
        private readonly ISessionService _sessionService;
        private readonly IProjectApi _projectApi;
        private readonly ILogger _logger;
        private readonly int _maxGuestsAllowedInProject;
        private readonly TimeSpan _expirationTime = TimeSpan.FromHours(8);

        public ProjectLobbyStateController(IRepositoryFactory repositoryFactory,
            ICacheAsync cache,
            IValidatorLocator validatorLocator,
            ISessionService sessionService,
            IProjectApi projectApi,
            IEventService eventService,
            ILoggerFactory loggerFactory,
            int maxGuestsAllowedInProject)
        {
            _cache = cache;
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

            var state = new ProjectLobbyState
            {
                ProjectId = projectId,
                LobbyState = LobbyState.Normal
            };

            await _cache.ItemSetAsync(LobbyStateKeyResolver.GetProjectLobbyStateKey(projectId), state, _expirationTime, CacheCommandOptions.None);
        }

        /// <inheritdoc />
        public async Task<ProjectLobbyState> RecalculateProjectLobbyStateAsync(Guid projectId)
        {
            var validationResult = _validatorLocator.Validate<ProjectIdValidator>(projectId);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            var projectResult = await _projectApi.GetProjectByIdAsync(projectId);

            if (!projectResult.IsSuccess() || projectResult.Payload == null)
            {
                if (projectResult.ResponseCode == HttpStatusCode.NotFound)
                {
                    throw new NotFoundException(ResponseReasons.NotFoundProject);
                }

                var saveState = projectResult.Payload != null;
                var result = await SetProjectLobbyStateToError(projectId, saveState);

                _logger.Error($"Failed to retrieve project: {projectId}. Message: {projectResult.ReasonPhrase}");
                return result;
            }

            var project = projectResult.Payload;

            IEnumerable<Participant> participantResult;
            var participantTask = _sessionService.GetParticipantsByGroupNameAsync($"{LegacyGroupPrefixes.Project}{projectId}");
            try
            {
                participantResult = await participantTask;
            }
            catch (Exception ex)
            {
                var result = await SetProjectLobbyStateToError(projectId, false);
                _logger.Error($"Failed to retrieve participants for project: {projectId}. Message: {ex.Message}", ex);
                return result;
            }

            var validProjectGuestsSessions = await _guestSessionRepository.GetItemsAsync(x => x.ProjectId == projectId && x.ProjectAccessCode == project.GuestAccessCode && x.GuestSessionState == GuestState.InProject);

            var currentValidProjectGuestSessions = validProjectGuestsSessions.GroupBy(s => s.UserId)
                .OrderBy(gs => gs.Key)
                .Select(gs => gs.OrderByDescending(x => x.CreatedDateTime)
                .First());

            var isHostPresent = participantResult?.Any(p => !p.IsGuest) ?? false;
            var isGuestLimitReached = currentValidProjectGuestSessions.Count() >= _maxGuestsAllowedInProject;

            return await UpsertProjectLobbyStateAsync(projectId, new ProjectLobbyState()
            {
                ProjectId = projectId,
                LobbyState = CalculateLobbyState(isGuestLimitReached, isHostPresent)
            });
        }

        public async Task<ProjectLobbyState> UpsertProjectLobbyStateAsync(Guid projectId, ProjectLobbyState projectLobbyState)
        {
            string key = LobbyStateKeyResolver.GetProjectLobbyStateKey(projectId);

            await _cache.ItemSetAsync(key, projectLobbyState, _expirationTime);

            _eventService.Publish(EventNames.ProjectStatusUpdated, projectLobbyState);

            return projectLobbyState;
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

            var result = await _cache.ItemGetAsync(new List<string>() {LobbyStateKeyResolver.GetProjectLobbyStateKey(projectId)}, typeof(ProjectLobbyState));
            if (result != null && result.Any())
            {
                return result.FirstOrDefault() as ProjectLobbyState;
            }

            return await RecalculateProjectLobbyStateAsync(projectId);
        }

        /// <inheritdoc />
        public async Task DeleteProjectLobbyStateAsync(Guid projectId)
        {
            await _cache.KeyDeleteAsync(new List<string> {LobbyStateKeyResolver.GetProjectLobbyStateKey(projectId)});
        }

        private async Task<ProjectLobbyState> SetProjectLobbyStateToError(Guid projectId, bool saveState = true)
        {
            var state = new ProjectLobbyState
            {
                ProjectId = projectId,
                LobbyState = LobbyState.Error
            };

            return saveState ? await UpsertProjectLobbyStateAsync(projectId, state) : state;
        }
    }
}
