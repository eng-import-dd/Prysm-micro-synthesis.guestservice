using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Synthesis.Cache;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Enumerations;
using Synthesis.GuestService.InternalApi.Constants;
using Synthesis.GuestService.InternalApi.Enums;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.Utilities.Interfaces;
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

namespace Synthesis.GuestService.Controllers
{
    public class ProjectLobbyStateController : IProjectLobbyStateController
    {
        private readonly ICacheSelector _cacheSelector;
        private readonly IRepository<GuestSession> _guestSessionRepository;
        private readonly IEventService _eventService;
        private readonly IValidatorLocator _validatorLocator;
        private readonly ISessionService _sessionService;
        private readonly IProjectApi _serviceToServiceProjectApi;
        private readonly ILogger _logger;
        private readonly int _maxGuestsAllowedInProject;
        private readonly TimeSpan _expirationTime = TimeSpan.FromHours(8);

        public ProjectLobbyStateController(IRepositoryFactory repositoryFactory,
            ICacheSelector cacheSelector,
            IValidatorLocator validatorLocator,
            ISessionService sessionService,
            IProjectApi serviceToServiceProjectApi,
            IEventService eventService,
            ILoggerFactory loggerFactory,
            int maxGuestsAllowedInProject)
        {
            _cacheSelector = cacheSelector;
            _validatorLocator = validatorLocator;
            _sessionService = sessionService;
            _serviceToServiceProjectApi = serviceToServiceProjectApi;
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

            await _cacheSelector[CacheConnection.General].ItemSetAsync(LobbyStateKeyResolver.GetProjectLobbyStateKey(projectId), state, _expirationTime);
        }

        /// <inheritdoc />
        public async Task<ProjectLobbyState> RecalculateProjectLobbyStateAsync(Guid projectId)
        {
            var validationResult = _validatorLocator.Validate<ProjectIdValidator>(projectId);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            var projectResult = await _serviceToServiceProjectApi.GetProjectByIdAsync(projectId);

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

            await _cacheSelector[CacheConnection.General].ItemSetAsync(key, projectLobbyState, _expirationTime);

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

            var result = await _cacheSelector[CacheConnection.General].ItemGetAsync(LobbyStateKeyResolver.GetProjectLobbyStateKey(projectId), typeof(ProjectLobbyState));
            if (result != null)
            {
                return result as ProjectLobbyState;
            }

            return await RecalculateProjectLobbyStateAsync(projectId);
        }

        /// <inheritdoc />
        public async Task DeleteProjectLobbyStateAsync(Guid projectId)
        {
            await _cacheSelector[CacheConnection.General].KeyDeleteAsync(LobbyStateKeyResolver.GetProjectLobbyStateKey(projectId));
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
