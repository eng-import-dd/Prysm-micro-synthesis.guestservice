using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation.Results;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Dao.Models;
using Synthesis.GuestService.Requests;
using Synthesis.GuestService.Responses;
using Synthesis.GuestService.Validators;
using Synthesis.GuestService.Workflow.ServiceInterop;
using Synthesis.GuestService.Workflow.ServiceInterop.Responses;
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
        /// <param name="emailUtility"></param>
        public GuestSessionController(
            IRepositoryFactory repositoryFactory,
            IValidatorLocator validatorLocator,
            IEventService eventService,
            ILogger logger,
            IProjectInterop projectInterop,
            ISettingsInterop settingsInterop,
            IUserInterop userInterop,
            IParticipantInterop participantInterop,
            IEmailUtility emailUtility)
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
            _logger = logger;
            _projectInterop = projectInterop;
            _settingsInterop = settingsInterop;
            _userInterop = userInterop;
            _participantInterop = participantInterop;

            _emailUtility = emailUtility;
        }

        public async Task<GuestCreationResponse> CreateGuestAsync(GuestCreationRequest request)
        {
            var emailValidationResult = _validatorLocator.Validate<EmailValidator>(request.Email);
            if (!emailValidationResult.IsValid)
            {
                _logger.Error("Failed to validate the email address while attempting to create a new guest.");
                throw new ValidationFailedException(emailValidationResult.Errors);
            }

            // Create default response
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
                var throwAwayPassword = await _userInterop.GenerateRandomPassword(64);
                request.Password = throwAwayPassword;
                request.PasswordConfirmation = throwAwayPassword;
            }

            var isUniqueEmail = await _userInterop.IsUniqueEmail(request.Email);
            if (!isUniqueEmail)
            {
                response.ResultCode = CreateGuestResponseCode.EmailIsNotUnique;
                return response;
            }

            var isUniqueUsername = await _userInterop.IsUniqueUsername(request.Email);
            if (!isUniqueUsername)
            {
                response.ResultCode = CreateGuestResponseCode.UsernameIsNotUnique;
                return response;
            }

            PasswordUtility.HashAndSalt(request.Password, out var passwordHash, out var passwordSalt);

            var provisionGuestUserResult = await _userInterop.ProvisionGuestUser(request.FirstName, request.LastName, request.Email, passwordHash, passwordSalt, request.IsIdpUser);

            if (provisionGuestUserResult == ProvisionGuestUserReturnCode.SucessEmailVerificationNeeded)
            {
                response.ResultCode = CreateGuestResponseCode.SucessEmailVerificationNeeded;

                var sendVerificationEmailResponse = await SendVerificationEmailAsync(new GuestVerificationEmail
                {
                    FirstName = request.FirstName,
                    Email = request.Email,
                    ProjectAccessCode = request.ProjectAccessCode,
                    LastName = request.LastName
                });

                if (sendVerificationEmailResponse.SendVerificationStatus != SendVerificationResult.Success)
                {
                    //TODO: What should we do here if SendVerificaitonEmail Fails?
                }

                return response;
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

            var isGuestLimitReached = await GetProjectActiveGuestSessionCountAsync(projectId) >= MaxGuestsAllowedInProject;
            var isHostPresent = await IsHostCurrentlyPresentInProjectAsync(projectId);

            var lobbyStatus = ProjectStatus.CalculateLobbyStatus(isGuestLimitReached, isHostPresent);
            return new ProjectStatus(lobbyStatus);
        }

        public async Task<GuestVerificationEmail> SendVerificationEmailAsync(GuestVerificationEmail guestVerificationEmail)
        {
            var emailValidationResult = _validatorLocator.Validate<EmailValidator>(guestVerificationEmail.Email);
            if (!emailValidationResult.IsValid)
            {
                _logger.Error("Failed to validate the email address while attempting to send a verification email.");
                throw new ValidationFailedException(emailValidationResult.Errors);
            }

            try
            {
                var user = await _userInterop.GetUserAsync(guestVerificationEmail.Email);
                if (user?.IsEmailVerified == null || user.IsEmailVerified.Value)
                {
                    guestVerificationEmail.SendVerificationStatus = SendVerificationResult.EmailNotVerified;
                    return guestVerificationEmail;
                }

                if (user.VerificationEmailSentDateTime.HasValue && (DateTime.UtcNow - user.VerificationEmailSentDateTime.Value).TotalMinutes < 1)
                {
                    guestVerificationEmail.SendVerificationStatus = SendVerificationResult.MsgAlreadySentWithinLastMinute;
                    return guestVerificationEmail;
                }

                //TODO: Update the email utility call with the correct EmailVerificationId
                if (_emailUtility.SendVerifyAccountEmail(guestVerificationEmail.FirstName, guestVerificationEmail.Email,
                                                         guestVerificationEmail.ProjectAccessCode, "ReplaceThisWithEmailVerificationId"))
                {
                    guestVerificationEmail.SendVerificationStatus = SendVerificationResult.Success;
                    return guestVerificationEmail;
                }

                guestVerificationEmail.SendVerificationStatus = SendVerificationResult.FailedToSend;
                return guestVerificationEmail;
            }
            catch (Exception e)
            {
                _logger.Error("Error sending guest verification email: " + e.Message);
                guestVerificationEmail.SendVerificationStatus = SendVerificationResult.FailedToSend;
                return guestVerificationEmail;
            }
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
            var project = await _projectInterop.GetProjectByAccessCodeAsync(projectAccessCode);
            if (project == null)
            {
                response.ResultCode = VerifyGuestResponseCode.Failed;
                return response;
            }

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

            var settings = await _settingsInterop.GetUserSettingsAsync(project.AccountId);
            if (settings != null)
            {
                if (settings.IsGuestModeEnabled != true)
                {
                    response.ResultCode = VerifyGuestResponseCode.Failed;
                    return response;
                }
            }

            var user = await _userInterop.GetUserAsync(username);
            if (user == null)
            {
                response.ResultCode = VerifyGuestResponseCode.Failed;
                return response;
            }

            if (user.IsLocked == true)
            {
                response.ResultCode = VerifyGuestResponseCode.UserIsLocked;
                return response;
            }

            if (user.IsEmailVerified != true)
            {
                response.ResultCode = VerifyGuestResponseCode.EmailVerificationNeeded;
                return response;
            }

            if (user.AccountId != null)
            {
                response.ResultCode = VerifyGuestResponseCode.InvalidNotGuest;
                return response;
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
                _logger.Error($"Failed to retrieve the project with id {projectId} when verifying if host is present.");
                throw new NotFoundException($"Error retrieving project with id {projectId} while verifying if host is present.");
            }

            var participants = await _participantInterop.GetParticipantsByProjectId(projectId);
            if (participants == null)
            {
                _logger.Error($"Failed to retrieve the participants for projectId {projectId} when verifying if host is present.");
                throw new NotFoundException($"Error retrieving participants for project {projectId} while verifying if host is present.");
            }

            if (participants.Any())
            {
                return participants.Any(p => p.UserId == project.OwnerId);
            }

            _logger.Error($"There are no current participants for projectId {projectId} when verifying if host is present.");
            throw new NotFoundException($"There are no participants for project {projectId} while verifying if host is present.");
        }
    }
}