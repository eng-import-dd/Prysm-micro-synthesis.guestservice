using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentValidation.Results;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Dao.Models;
using Synthesis.GuestService.Extensions;
using Synthesis.GuestService.Requests;
using Synthesis.GuestService.Responses;
using Synthesis.GuestService.Validators;
using Synthesis.GuestService.Workflow.ApiWrappers;
using Synthesis.GuestService.Workflow.Utilities;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;

namespace Synthesis.GuestService.Workflow.Controllers
{
    /// <summary>
    ///     Represents a controller for GuestSession resources.
    /// </summary>
    /// <seealso cref="IGuestSessionController" />
    public class GuestSessionController : IGuestSessionController
    {
        private const int MaxGuestsAllowedInProject = 10;
        private readonly IEmailUtility _emailUtility;
        private readonly IEventService _eventService;
        private readonly IRepository<GuestSession> _guestSessionRepository;
        private readonly ILogger _logger;
        private readonly IParticipantApiWrapper _participantApi;
        private readonly IPasswordUtility _passwordUtility;
        private readonly IProjectApiWrapper _projectApi;
        private readonly ISettingsApiWrapper _settingsApi;
        private readonly IPrincipalApiWrapper _userApi;
        private readonly IValidatorLocator _validatorLocator;

        /// <summary>
        ///     Initializes a new instance of the <see cref="GuestSessionController" /> class.
        /// </summary>
        /// <param name="repositoryFactory">The repository factory.</param>
        /// <param name="validatorLocator">The validator locator.</param>
        /// <param name="eventService">The event service.</param>
        /// <param name="loggerFactory">The logger.</param>
        /// <param name="projectApi"></param>
        /// <param name="settingsApi"></param>
        /// <param name="userApi"></param>
        /// <param name="participantApi"></param>
        /// <param name="emailUtility"></param>
        /// <param name="passwordUtility"></param>
        public GuestSessionController(
            IRepositoryFactory repositoryFactory,
            IValidatorLocator validatorLocator,
            IEventService eventService,
            ILoggerFactory loggerFactory,
            IEmailUtility emailUtility,
            IPasswordUtility passwordUtility,
            IProjectApiWrapper projectApi,
            IParticipantApiWrapper participantApi,
            IPrincipalApiWrapper userApi,
            ISettingsApiWrapper settingsApi)
        {
            try
            {
                _guestSessionRepository = repositoryFactory.CreateRepository<GuestSession>();
            }
            catch (Exception)
            {
                // supressing the repository exceptions for initial testing
            }

            _validatorLocator = validatorLocator;
            _eventService = eventService;
            _logger = loggerFactory.GetLogger(this);

            _emailUtility = emailUtility;
            _passwordUtility = passwordUtility;

            _projectApi = projectApi;
            _participantApi = participantApi;
            _userApi = userApi;
            _settingsApi = settingsApi;
        }

        public async Task<GuestCreationResponse> CreateGuestAsync(GuestCreationRequest request)
        {
            var emailValidationResult = _validatorLocator.Validate<EmailValidator>(request.Email);
            if (!emailValidationResult.IsValid)
            {
                _logger.Error("Failed to validate the email address while attempting to create a new guest.");
                throw new ValidationFailedException(emailValidationResult.Errors);
            }

            var response = new GuestCreationResponse
            {
                SynthesisUser = null,
                ResultCode = CreateGuestResponseCode.Failed
            };

            var guestVerificationResponse = await VerifyGuestAsync(request.Email, request.ProjectAccessCode);
            if (!(guestVerificationResponse.ResultCode == VerifyGuestResponseCode.Success || guestVerificationResponse.ResultCode == VerifyGuestResponseCode.SuccessNoUser))
            {
                response.ResultCode = guestVerificationResponse.ResultCode == VerifyGuestResponseCode.EmailVerificationNeeded ? CreateGuestResponseCode.UserExists : CreateGuestResponseCode.Unauthorized;
                return response;
            }

            request.FirstName = request.FirstName?.Trim();
            request.LastName = request.LastName?.Trim();
            if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            {
                response.ResultCode = CreateGuestResponseCode.FirstOrLastNameIsNull;
                return response;
            }

            if (!EmailValidator.IsValid(request.Email))
            {
                response.ResultCode = CreateGuestResponseCode.InvalidEmail;
                return response;
            }

            if (request.IsIdpUser)
            {
                var throwAwayPassword = _passwordUtility.GenerateRandomPassword(64);
                request.Password = throwAwayPassword;
                request.PasswordConfirmation = throwAwayPassword;
            }

            var userRequest = new UserRequest
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                Password = request.Password,
                IsIdpUser = request.IsIdpUser
            };

            var provisionGuestUserResult = await _userApi.ProvisionGuestUserAsync(userRequest);
            if (provisionGuestUserResult.Payload.ProvisionReturnCode == ProvisionGuestUserReturnCode.SucessEmailVerificationNeeded)
            {
                response.ResultCode = CreateGuestResponseCode.SucessEmailVerificationNeeded;

                var sendVerificationEmailResponse = await SendVerificationEmailAsync(new GuestVerificationEmailRequest
                {
                    FirstName = request.FirstName,
                    Email = request.Email,
                    ProjectAccessCode = request.ProjectAccessCode,
                    LastName = request.LastName
                });

                if (!sendVerificationEmailResponse.IsEmailVerified || sendVerificationEmailResponse.MessageSentRecently)
                {
                    response.ResultCode = CreateGuestResponseCode.SucessEmailVerificationNeeded;
                }
            }

            response.ResultCode = CreateGuestResponseCode.Success;
            return response;
        }

        public async Task<GuestSession> CreateGuestSessionAsync(GuestSession model)
        {
            var validationResult = _validatorLocator.Validate<GuestSessionValidator>(model);
            if (!validationResult.IsValid)
            {
                _logger.Error("Validation failed while attempting to create a GuestSession resource.");
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
                _logger.Error("Failed to validate the resource id while attempting to retrieve a GuestSession resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var result = await _guestSessionRepository.GetItemAsync(id);
            if (result != null)
            {
                return result;
            }

            _logger.Error($"A GuestSession resource could not be found for id {id}");
            throw new NotFoundException("GuestSession could not be found");
        }

        public async Task<IEnumerable<GuestSession>> GetGuestSessionsByProjectIdAsync(Guid projectId)
        {
            var validationResult = _validatorLocator.Validate<ProjectIdValidator>(projectId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the projectId while attempting to retrieve GuestSession resources.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var result = await _guestSessionRepository.GetItemsAsync(x => x.ProjectId == projectId);
            if (result != null)
            {
                return result;
            }

            _logger.Error($"GuestSession resources could not be found for projectId {projectId}");
            throw new NotFoundException("GuestSessions could not be found");
        }

        public async Task<ProjectStatus> GetProjectStatusAsync(Guid projectId)
        {
            var validationResult = _validatorLocator.Validate<ProjectIdValidator>(projectId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the projectId while attempting to reset the project access code.");
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
                _logger.Error($"Failed to retrieve the participants for projectId {projectId} when verifying if host is present.");
                throw new NotFoundException($"Project {projectId} does not exist");
            }

            projectResult.VerifySuccess($"Failed to retrieve project {projectId} when verifying if host is present.");
            participantResult.VerifySuccess($"Failed to retrieve participants for project {projectId} when verifying if host is present.");

            var project = projectResult.Payload;
            var participants = participantResult.Payload.ToList();

            var isHostPresent = participants.Any(p => p.UserId == project.OwnerId);
            var isGuestLimitReached = projectGuestsResult.Count(g => g.GuestSessionState == GuestState.InProject) >= MaxGuestsAllowedInProject;

            var lobbyStatus = ProjectStatus.CalculateLobbyStatus(isGuestLimitReached, isHostPresent);
            return new ProjectStatus(lobbyStatus);
        }

        public async Task<GuestVerificationEmailResponse> SendVerificationEmailAsync(GuestVerificationEmailRequest guestVerificationEmailRequest)
        {
            var emailValidationResult = _validatorLocator.Validate<EmailValidator>(guestVerificationEmailRequest.Email);
            if (!emailValidationResult.IsValid)
            {
                _logger.Error("Failed to validate the email address while attempting to send a verification email.");
                throw new ValidationFailedException(emailValidationResult.Errors);
            }

            var guestVerificationEmailResponse = new GuestVerificationEmailResponse
            {
                Email = guestVerificationEmailRequest.Email,
                FirstName = guestVerificationEmailRequest.FirstName,
                LastName = guestVerificationEmailRequest.LastName,
                ProjectAccessCode = guestVerificationEmailRequest.ProjectAccessCode
            };

            var user = await _userApi.GetUserAsync(new UserRequest { Email = guestVerificationEmailRequest.Email });
            if (user.Payload.IsEmailVerified == true)
            {

                guestVerificationEmailResponse.IsEmailVerified = true;
                return guestVerificationEmailResponse;
            }

            if (user.Payload.VerificationEmailSentDateTime.HasValue && (DateTime.UtcNow - user.Payload.VerificationEmailSentDateTime.Value).TotalMinutes < 1)
            {
                guestVerificationEmailResponse.MessageSentRecently = true;
                return guestVerificationEmailResponse;
            }

            _emailUtility.SendVerifyAccountEmail(guestVerificationEmailRequest.FirstName, guestVerificationEmailRequest.Email,
                                                 guestVerificationEmailRequest.ProjectAccessCode, user.Payload.EmailVerificationId.ToString());
            return guestVerificationEmailResponse;
        }

        public async Task<GuestSession> UpdateGuestSessionAsync(GuestSession guestSessionModel)
        {
            var errors = GetFailures(Tuple.Create<Type, object>(typeof(GuestSessionIdValidator), guestSessionModel.Id),
                                     Tuple.Create<Type, object>(typeof(GuestSessionValidator), guestSessionModel));
            if (errors.Any())
            {
                _logger.Error("Failed to validate the resource id and/or resource while attempting to update a GuestSession resource.");
                throw new ValidationFailedException(errors);
            }

            var result = await _guestSessionRepository.UpdateItemAsync(guestSessionModel.Id, guestSessionModel);

            _eventService.Publish(EventNames.GuestSessionUpdated, result);

            return result;
        }

        public async Task<GuestVerificationResponse> VerifyGuestAsync(string username, string projectAccessCode)
        {
            var errors = GetFailures(Tuple.Create<Type, object>(typeof(EmailValidator), username),
                                     Tuple.Create<Type, object>(typeof(ProjectAccessCodeValidator), projectAccessCode));

            if (errors.Any())
            {
                _logger.Error("Failed to validate the guest verification request.");
                throw new ValidationFailedException(errors);
            }

            var response = new GuestVerificationResponse();
            var projectResponse = await _projectApi.GetProjectByAccessCodeAsync(projectAccessCode);
            if (projectResponse == null)
            {
                response.ResultCode = VerifyGuestResponseCode.Failed;
                return response;
            }

            var project = projectResponse.Payload;

            response.AccountId = project.TenantId;
            response.AssociatedProject = project;
            response.ProjectAccessCode = projectAccessCode;
            response.ProjectName = project.Name;
            response.UserId = project.OwnerId;
            response.Username = username;

            if (project.IsGuestModeEnabled != true)
            {
                response.ResultCode = VerifyGuestResponseCode.Failed;
                return response;
            }

            var userResponse = await _userApi.GetUserAsync(new UserRequest { UserName = username });
            if (userResponse == null)
            {
                response.ResultCode = VerifyGuestResponseCode.Failed;
                return response;
            }

            var user = userResponse.Payload;

            if (user.IsLocked)
            {
                response.ResultCode = VerifyGuestResponseCode.UserIsLocked;
                return response;
            }

            if (user.IsEmailVerified != true)
            {
                response.ResultCode = VerifyGuestResponseCode.EmailVerificationNeeded;
                return response;
            }

            if (user.TenantId != null)
            {
                response.ResultCode = VerifyGuestResponseCode.InvalidNotGuest;
                return response;
            }

            var settingsResponse = await _settingsApi.GetSettingsAsync(user.Id);
            if (settingsResponse != null)
            {
                var settings = settingsResponse.Payload;
                if (settings.IsGuestModeEnabled != true)
                {
                    response.ResultCode = VerifyGuestResponseCode.Failed;
                    return response;
                }
            }

            response.ResultCode = VerifyGuestResponseCode.Success;
            return response;
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

        public async Task DeleteGuestSessionsForProject(Guid projectId, bool onlyKickGuestsInProject)
        {
            var guestSessions = (await _guestSessionRepository.GetItemsAsync(x => x.ProjectId == projectId)).ToList();

            guestSessions
                .Where(x => onlyKickGuestsInProject && x.GuestSessionState == GuestState.InProject ||
                            !onlyKickGuestsInProject && x.GuestSessionState != GuestState.Ended)
                .ToList()
                .ForEach(async session =>
                {
                    session.GuestSessionState = GuestState.Ended;
                    await _guestSessionRepository.UpdateItemAsync(session.Id, session);
                });

            _eventService.Publish(EventNames.GuestSessionsForProjectDeleted, new GuidEvent(projectId));
        }
    }
}