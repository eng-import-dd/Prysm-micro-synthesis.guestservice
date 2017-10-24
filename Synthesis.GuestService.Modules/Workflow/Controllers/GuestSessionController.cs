using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation.Results;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Dao.Models;
using Synthesis.GuestService.Responses;
using Synthesis.GuestService.Validators;
using Synthesis.GuestService.Workflow.Interfaces;
using Synthesis.GuestService.Workflow.ServiceInterop;
using Synthesis.GuestService.Workflow.ServiceInterop.Responses;
using Synthesis.Http.Microservice;
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
        private readonly IEventService _eventService;
        private readonly IRepository<GuestSession> _guestSessionRepository;
        private readonly ILogger _logger;
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
        public GuestSessionController(
            IRepositoryFactory repositoryFactory,
            IValidatorLocator validatorLocator,
            IEventService eventService,
            ILogger logger,
            IProjectInterop projectInterop,
            ISettingsInterop settingsInterop,
            IUserInterop userInterop)
        {
            _guestSessionRepository = repositoryFactory.CreateRepository<GuestSession>();

            _validatorLocator = validatorLocator;
            _eventService = eventService;
            _logger = logger;
            _projectInterop = projectInterop;
            _settingsInterop = settingsInterop;
            _userInterop = userInterop;
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

        public async Task<GuestVerificationResponse> VerifyGuestAsync(string username, string projectAccessCode)
        {
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

            //todo return success
            throw new NotImplementedException();
        }

        // -- In Progress
        public async Task<Guest> CreateGuestAsync(Guest guest)
        {
            //todo delete me
            await Task.Yield();

            var emailValidationResult = _validatorLocator.Validate<EmailValidator>(guest.Email);
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

            return null;
        }

        // -- In Progress
        public async Task<ProjectStatus> GetProjectStatusAsync(Guid projectId)
        {
            //todo delete me
            await Task.Yield();

            var validationResult = _validatorLocator.Validate<ProjectIdValidator>(projectId);
            if (!validationResult.IsValid)
            {
                _logger.Warning("Failed to validate the projectId while attempting to reset the project access code.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            #region COLLABORATION SERVICE CODE

            //try
            //{
            //    string key = KeyResolver.ProjectStatus(projectId);
            //    var cache = _cacheSelector[CacheConnection.General];

            //    ProjectStatusDTO projectStatus;
            //    if (cache.TryItemGet(key, out projectStatus))
            //    {
            //        cache.KeySetExpiration(key, _expirationTime, CacheCommandOptions.FireAndForget);
            //        return ServiceResult.Success(projectStatus);
            //    }

            //    // isGuestLimitReached
            //    var guestsInProject = GetProjectGuests(projectId)
            //        .Payload.GuestSessions.Where(g => g.GuestSessionStateId == GuestState.InProject);
            //    var isGuestLimitReached = guestsInProject.Count() >= MAX_GUESTS_IN_PROJECT;

            //    // isHostPresent
            //    var participantsKey = GroupResolver.Project(projectId);
            //    var particiantsExists = cache.KeyExists(participantsKey);
            //    var isHostPresent = false;
            //    if (particiantsExists)
            //    {
            //        var participantsInGroup = cache.HashGetValues<ParticipantDTO>(participantsKey).ToList();

            //        isHostPresent = participantsInGroup.Any(p => !p.IsGuest);
            //    }

            //    var lobbyStatus = ProjectStatusDTO.CalculateLobbyStatus(isGuestLimitReached, isHostPresent);
            //    projectStatus = new ProjectStatusDTO(lobbyStatus);

            //    cache.ItemSet(key, projectStatus, _expirationTime);

            //    return ServiceResult.Success(projectStatus, "Request successful");
            //}
            //catch (Exception ex)
            //{
            //    return ServiceResult.Failed<ProjectStatusDTO>($"Request not successful. {ex.Message}");
            //}

            #endregion

            return null;
        }

        // -- In Progress
        public async Task<Project> ResetAccessCodeAsync(Guid projectId)
        {
            //todo delete me
            await Task.Yield();

            var validationResult = _validatorLocator.Validate<ProjectIdValidator>(projectId);
            if (!validationResult.IsValid)
            {
                _logger.Warning("Failed to validate the projectId while attempting to reset the project access code.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            // 1. Kick guests from project
            // 2. Generate a new access code and update the project with the code
            // 3. Broadcast guest list changed
            // 4. Notify project changed
            // 5. Return Project with new access code

            #region COLLABORATION SERVICE CODE

            //var result = new ServiceResult<ProjectDTO>();
            //try
            //{
            //    KickGuestsFromProject(projectId, true);

            //    string oldAccessCode;
            //    Project updatedProject = _databaseService.ResetProjectAccessCode(projectId, out oldAccessCode);

            //    if (updatedProject != null)
            //    {
            //        var cache = _cacheSelector[CacheConnection.General];
            //        cache.KeyDelete(KeyResolver.GuestAccessCode(oldAccessCode));
            //        cache.KeyDelete(KeyResolver.Project(projectId));

            //        ClearGuestListCache(projectId);
            //        BroadcastGuestListChanged(projectId);

            //        var updatedProjectDTO = new ProjectDTO
            //        {
            //            ProjectID = updatedProject.ProjectID,
            //            Name = updatedProject.Name,
            //            DateCreated = updatedProject.DateCreated,
            //            DateStarted = updatedProject.DateStarted,
            //            ManagerUserID = updatedProject.ManagerUserID,
            //            ProductID = updatedProject.ProductID,
            //            DateLastAccessed = updatedProject.DateLastAccessed,
            //            AspectRatioId = updatedProject.AspectRatioId,
            //            IsGuestModeEnabled = updatedProject.IsGuestModeEnabled,
            //            GuestAccessCode = updatedProject.GuestAccessCode,
            //            GuestAccessCodeCreatedDateTime = updatedProject.GuestAccessCodeCreatedDateTime
            //        };

            //        NotifyProjectChanged(updatedProjectDTO);

            //        result.Payload = updatedProjectDTO;
            //        result.Message = string.Empty;
            //        result.ResultCode = ResultCode.Success;
            //    }
            //    else
            //    {
            //        result.Payload = null;
            //        result.Message = string.Format("Project record with id {0} not found in {1}", projectId, MethodBase.GetCurrentMethod().Name);
            //        result.ResultCode = ResultCode.RecordNotFound;
            //    }
            //}
            //catch (Exception ex)
            //{
            //    LogError(ex, MethodBase.GetCurrentMethod().Name);
            //    result.Payload = null;
            //    result.Message = ex.ToString();
            //    result.ResultCode = ResultCode.Failed;
            //}
            //return result;

            #endregion

            return null;
        }

        // -- In Progress
        public async Task<GuestVerificationEmail> SendVerificationEmailAsync(GuestVerificationEmail guestVerificationmail)
        {
            //todo delete me
            await Task.Yield();

            var emailValidationResult = _validatorLocator.Validate<EmailValidator>(guestVerificationmail.Email);
            if (!emailValidationResult.IsValid)
            {
                _logger.Warning("Failed to validate the email address while attempting to send a verification email.");
                throw new ValidationFailedException(emailValidationResult.Errors);
            }

            #region COLLABORATION SERVICE CODE

            //try
            //{
            //    var user = _databaseService.GetSynthesisUserByEmail(sendVerificationEmailRequest.Email);
            //    if (user == null || user.IsEmailVerified == null || user.IsEmailVerified.Value || user.EmailVerificationId == null)
            //    {
            //        return new ServiceResult<bool>
            //        {
            //            Message = "Failed to send verification email",
            //            Payload = false,
            //            ResultCode = ResultCode.Failed
            //        };
            //    }

            //    if (user.VerificationEmailSentDateTime.HasValue
            //        && (DateTime.UtcNow - user.VerificationEmailSentDateTime.Value).TotalMinutes < 1)
            //    {
            //        return new ServiceResult<bool>
            //        {
            //            Message = "Failed to send verification email because one was sent less than a minute ago",
            //            Payload = false,
            //            ResultCode = ResultCode.Failed
            //        };
            //    }

            //    if (_emailUtility.SendVerifyAccountEmail(user.FirstName, sendVerificationEmailRequest.Email,
            //        sendVerificationEmailRequest.ProjectAccessCode, user.EmailVerificationId.ToString()))
            //    {
            //        _databaseService.UpdateVerificationEmailSentDateTime(sendVerificationEmailRequest.Email);
            //    }

            //    return new ServiceResult<bool>
            //    {
            //        Message = "Successfully sent verification email",
            //        Payload = true,
            //        ResultCode = ResultCode.Success
            //    };
            //}
            //catch (Exception ex)
            //{
            //    LogError(ex, MethodBase.GetCurrentMethod().Name);
            //    return new ServiceResult<bool>
            //    {
            //        Message = "Fail to send verification email : " + ex,
            //        Payload = false,
            //        ResultCode = ResultCode.Failed
            //    };
            //}

            #endregion

            return null;
        }

        private static string GenerateNewProjectAccessCode()
        {
            return ((long)(new Random().NextDouble() * 1000000000) + 8999999999).ToString();
        }

        private static bool IsSuccess(MicroserviceResponse response)
        {
            var code = response.ResponseCode;
            return (int)code >= 200
                   && (int)code <= 299;
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
    }
}