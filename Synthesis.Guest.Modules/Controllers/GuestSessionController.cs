using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.ApiWrappers.Interfaces;
using Synthesis.GuestService.ApiWrappers.Requests;
using Synthesis.GuestService.ApiWrappers.Responses;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Enums;
using Synthesis.GuestService.Models;
using Synthesis.GuestService.Requests;
using Synthesis.GuestService.Responses;
using Synthesis.GuestService.Utilities.Interfaces;
using Synthesis.GuestService.Validators;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;

namespace Synthesis.GuestService.Controllers
{
    /// <summary>
    ///     Represents a controller for GuestSession resources.
    /// </summary>
    /// <seealso cref="IGuestSessionController" />
    public class GuestSessionController : IGuestSessionController
    {
        private readonly IEmailUtility _emailUtility;
        private readonly IEventService _eventService;
        private readonly IRepository<GuestSession> _guestSessionRepository;
        private readonly ILogger _logger;
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
            IPrincipalApiWrapper userApi,
            ISettingsApiWrapper settingsApi)
        {
            _guestSessionRepository = repositoryFactory.CreateRepository<GuestSession>();

            _validatorLocator = validatorLocator;
            _eventService = eventService;
            _logger = loggerFactory.GetLogger(this);

            _emailUtility = emailUtility;
            _passwordUtility = passwordUtility;

            _projectApi = projectApi;
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

        public async Task DeleteGuestSessionsForProjectAsync(Guid projectId, bool onlyKickGuestsInProject)
        {
            var guestSessions = (await _guestSessionRepository.GetItemsAsync(x => x.ProjectId == projectId)).ToList();

            var guestSessionTasks = guestSessions
                .Where(x => onlyKickGuestsInProject && x.GuestSessionState == GuestState.InProject ||
                    !onlyKickGuestsInProject && x.GuestSessionState != GuestState.Ended)
                .Select(session =>
                {
                    session.GuestSessionState = GuestState.Ended;
                    return _guestSessionRepository.UpdateItemAsync(session.Id, session);
                });

            await Task.WhenAll(guestSessionTasks);

            _eventService.Publish(EventNames.GuestSessionsForProjectDeleted, new GuidEvent(projectId));
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
            var validationResult = _validatorLocator.ValidateMany(new Dictionary<Type, object>
            {
                { typeof(GuestSessionIdValidator), guestSessionModel.Id },
                { typeof(GuestSessionValidator), guestSessionModel }
            });

            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id and/or resource while attempting to update a GuestSession resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var result = await _guestSessionRepository.UpdateItemAsync(guestSessionModel.Id, guestSessionModel);

            _eventService.Publish(EventNames.GuestSessionUpdated, result);

            return result;
        }

        public async Task<GuestVerificationResponse> VerifyGuestAsync(string username, string projectAccessCode)
        {
            var validationResult = _validatorLocator.ValidateMany(new Dictionary<Type, object>
            {
                { typeof(EmailValidator), username },
                { typeof(ProjectAccessCodeValidator), projectAccessCode }
            });

            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the guest verification request.");
                throw new ValidationFailedException(validationResult.Errors);
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
    }
}