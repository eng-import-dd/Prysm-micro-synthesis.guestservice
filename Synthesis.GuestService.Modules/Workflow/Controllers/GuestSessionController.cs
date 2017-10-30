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

        // TODO: In progress...
        public async Task<GuestCreationResponse> CreateGuestAsync(GuestCreationRequest model)
        {
            //TODO: Delete me
            await Task.Yield();
            

            var emailValidationResult = _validatorLocator.Validate<EmailValidator>(model.Email);
            if (!emailValidationResult.IsValid)
            {
                _logger.Warning("Failed to validate the email address while attempting to create a new guest.");
                throw new ValidationFailedException(emailValidationResult.Errors);
            }

            #region COLLABORATION SERVICE CODE

            // Create default response
            //    var result = new ServiceResult<CreateGuestResponse>
            //    {
            //        ResultCode = ResultCode.Failed,
            //        Message = "Could not complete request.",
            //        Payload = new CreateGuestResponse
            //        {
            //            User = null,
            //            ResultCode = CreateGuestResponseCode.Failed
            //        }
            //    };

            //    try
            //    {
            //        // Guest verification check
            //        ServiceResult<VerifyGuestResponseInternal> verificationResult = VerifyGuest(new VerifyGuestRequest() { Username = createGuestRequest.Email, ProjectAccessCode = createGuestRequest.ProjectAccessCode });
            //        if (!(verificationResult.Payload.ResultCode == VerifyGuestResponseCode.Success || verificationResult.Payload.ResultCode == VerifyGuestResponseCode.SuccessNoUser))
            //        {
            //            result.Message = "Guest verification failed";
            //            result.Payload.ResultCode = (verificationResult.Payload.ResultCode == VerifyGuestResponseCode.EmailVerificationNeeded) ? CreateGuestResponseCode.UserExists : CreateGuestResponseCode.Unauthorized;
            //            return result;
            //        }

            //        createGuestRequest.FirstName = createGuestRequest.FirstName?.Trim();
            //        createGuestRequest.LastName = createGuestRequest.LastName?.Trim();

            //        if (string.IsNullOrWhiteSpace(createGuestRequest.FirstName) || string.IsNullOrWhiteSpace(createGuestRequest.LastName))
            //        {
            //            result.Message = "First and Last Name can not be null";
            //            result.Payload.ResultCode = CreateGuestResponseCode.FirstOrLastNameIsNull;
            //            return result;
            //        }

            //        if (!EmailValidator.IsValid(createGuestRequest.Email))
            //        {
            //            result.Message = "Email is not valid";
            //            result.Payload.ResultCode = CreateGuestResponseCode.InvalidEmail;
            //            return result;
            //        }

            //        if (isIdpUser)
            //        {
            //            var throwAwayPassword = UserService.GenerateRandomPassword(64);
            //            createGuestRequest.Password = throwAwayPassword;
            //            createGuestRequest.PasswordConfirmation = throwAwayPassword;
            //        }
            //        else
            //        {
            //            // check the configured password validation policy
            //            var defaultPasswordPolicy = new DefaultPasswordPolicy();

            //            if (!(defaultPasswordPolicy.IsValidPolicy(createGuestRequest.Password)))
            //            {
            //                result.Payload.ResultCode = CreateGuestResponseCode.InvalidPassword;
            //                result.Message = defaultPasswordPolicy.Description;
            //                return result;
            //            }

            //            if (createGuestRequest.Password != createGuestRequest.PasswordConfirmation)
            //            {
            //                result.Payload.ResultCode = CreateGuestResponseCode.PasswordConfirmationError;
            //                result.Message = "Password and PasswordConfirmation must match";
            //                return result;
            //            }
            //        }

            //        string passwordHash;
            //        string passwordSalt;
            //        PasswordUtility.HashAndSalt(createGuestRequest.Password, out passwordHash, out passwordSalt);

            //        ProvisionGuestUserResult dbResult = _databaseService.ProvisionGuestUser(createGuestRequest.FirstName, createGuestRequest.LastName, createGuestRequest.Email, passwordHash, passwordSalt, isIdpUser);

            //        if (dbResult.ReturnCode == ProvisionGuestUserReturnCode.EmailIsNotUnique)
            //        {
            //            result.Message = "Email is not unique";
            //            result.Payload.ResultCode = CreateGuestResponseCode.UserExists;
            //            return result;
            //        }

            //        if (dbResult.ReturnCode == ProvisionGuestUserReturnCode.UsernameIsNotUnique)
            //        {
            //            result.Message = "Username is not unique";
            //            result.Payload.ResultCode = CreateGuestResponseCode.UsernameIsNotUnique;
            //            return result;
            //        }

            //        if (dbResult.ReturnCode == ProvisionGuestUserReturnCode.Success || dbResult.ReturnCode == ProvisionGuestUserReturnCode.SucessEmailVerificationNeeded)
            //        {
            //            var userDTO = Mapper.Map<SynthesisUser, SynthesisUserDTO>(dbResult.User);
            //            if (userDTO == null)
            //            {
            //                result.Message = "Failed to create guest user!";
            //                result.Payload.ResultCode = CreateGuestResponseCode.Failed;
            //                return result;
            //            }

            //            result.Payload.ResultCode = dbResult.ReturnCode == ProvisionGuestUserReturnCode.SucessEmailVerificationNeeded
            //                ? CreateGuestResponseCode.SucessEmailVerificationNeeded
            //                : CreateGuestResponseCode.Success;

            //            result.Message = "CreateGuest";
            //            result.Payload.User = userDTO;

            //            result.ResultCode = ResultCode.Success;

            //            if (dbResult.ReturnCode == ProvisionGuestUserReturnCode.SucessEmailVerificationNeeded)
            //            {
            //                //TODO: What should we do here if SendVerificaitonEmail Fails?
            //                SendVerificationEmail(new SendVerificationEmailRequest
            //                {
            //                    Email = userDTO.Email,
            //                    ProjectAccessCode = createGuestRequest.ProjectAccessCode
            //                });
            //            }

            //            return result;
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        LogError(ex, MethodBase.GetCurrentMethod().Name);
            //        result.Message = ex.ToString();
            //    }
            //    return result;
            //}

            #endregion

            // TODO: Delete me
            return null;
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

        public async Task<GuestVerificationEmail> SendVerificationEmailAsync(GuestVerificationEmail guestVerificationEmail)
        {
            var emailValidationResult = _validatorLocator.Validate<EmailValidator>(guestVerificationEmail.Email);
            if (!emailValidationResult.IsValid)
            {
                _logger.Warning("Failed to validate the email address while attempting to send a verification email.");
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

                //TODO: Implement email utility to send the email here
                guestVerificationEmail.SendVerificationStatus = SendVerificationResult.Success;
                return guestVerificationEmail;
            }
            catch (Exception e)
            {
                _logger.Error("Error sending guest verification email: " + e.Message);
                guestVerificationEmail.SendVerificationStatus = SendVerificationResult.FailedToSend;
                return guestVerificationEmail;
            }
        }

        public async Task<GuestVerificationResponse> VerifyGuestAsync(string username, string projectAccessCode)
        {
            //TODO: Implement the error handling for this method
            var errors = GetFailures(Tuple.Create<Type, object>(typeof(EmailValidator), username),
                                     Tuple.Create<Type, object>(typeof(ProjectAccessCodeValidator), projectAccessCode));
            if (errors.Any())
            {
                _logger.Error("Failed to validate the guest verification request.");
                throw new ValidationFailedException(errors);
            }

            // -- Make the call to the Project service
            var project = await _projectInterop.GetProjectByAccessCodeAsync(projectAccessCode);
            if (project.IsGuestModeEnabled != true)
            {
                //todo fail
            }

            var settings = await _settingsInterop.GetUserSettingsAsync(project.AccountId);
            if (settings.IsGuestModeEnabled != true)
            {
                //todo fail
            }

            var user = await _userInterop.GetUserAsync(username);
            if (user.IsLocked == true
                && user.IsEmailVerified != true
                && user.AccountId != null)
            {
                //todo fail
            }

            //TODO: Return success
            throw new NotImplementedException();
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