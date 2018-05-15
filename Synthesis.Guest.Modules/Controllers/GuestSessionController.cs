using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.ApiWrappers.Interfaces;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Extensions;
using Synthesis.GuestService.InternalApi.Enums;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.InternalApi.Requests;
using Synthesis.GuestService.InternalApi.Responses;
using Synthesis.GuestService.Utilities.Interfaces;
using Synthesis.GuestService.Validators;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Api;

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
        private readonly IRepository<GuestInvite> _guestInviteRepository;
        private readonly ILogger _logger;
        private readonly IProjectApi _projectApi;
        private readonly ISettingsApiWrapper _settingsApi;
        private readonly IUserApi _userApi;
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
        public GuestSessionController(
            IRepositoryFactory repositoryFactory,
            IValidatorLocator validatorLocator,
            IEventService eventService,
            ILoggerFactory loggerFactory,
            IEmailUtility emailUtility,
            IProjectApi projectApi,
            IUserApi userApi,
            ISettingsApiWrapper settingsApi)
        {
            _guestSessionRepository = repositoryFactory.CreateRepository<GuestSession>();
            _guestInviteRepository = repositoryFactory.CreateRepository<GuestInvite>();

            _validatorLocator = validatorLocator;
            _eventService = eventService;
            _logger = loggerFactory.GetLogger(this);

            _emailUtility = emailUtility;

            _projectApi = projectApi;
            _userApi = userApi;
            _settingsApi = settingsApi;
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

            // TODO: IsEmailVerified & VerificationEmailSentDateTime are no longer in the payload coming from the microservice. They are stored in the policy_db.users table
            //var user = await _userApi.GetUserAsync(new UserRequest { Email = guestVerificationEmailRequest.Email });
            //if (user.Payload.IsEmailVerified == true)
            //{
            //    guestVerificationEmailResponse.IsEmailVerified = true;
            //    return guestVerificationEmailResponse;
            //}

            //if (user.Payload.VerificationEmailSentDateTime.HasValue && (DateTime.UtcNow - user.Payload.VerificationEmailSentDateTime.Value).TotalMinutes < 1)
            //{
            //    guestVerificationEmailResponse.MessageSentRecently = true;
            //    return guestVerificationEmailResponse;
            //}

            // TODO: EmailVerificationId is no longer in the payload coming from the microservice. It is stored in the policy_db.users table. Also need to know where or add where the EmailVerificationId is generated.
            //_emailUtility.SendVerifyAccountEmail(
            //    guestVerificationEmailRequest.FirstName,
            //    guestVerificationEmailRequest.Email,
            //    guestVerificationEmailRequest.ProjectAccessCode,
            //    user.Payload.EmailVerificationId.ToString());

            // TODO: Remove me once the above logic is back in place- this is just here so the async function has an await
            await Task.Delay(0);

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

        public async Task<SendHostEmailResponse> EmailHostAsync(string accessCode, Guid sendingUserId)
        {
            var codeValidationResult = _validatorLocator.Validate<ProjectAccessCodeValidator>(accessCode);
            if (!codeValidationResult.IsValid)
            {
                _logger.Error($"Failed to validate the project access code {accessCode} while attempting to email the host");
                throw new ValidationFailedException(codeValidationResult.Errors);
            }

            var sendingUser = (await _userApi.GetUserAsync(sendingUserId)).Payload;
            if (sendingUser == null)
            {
                throw new NotFoundException($"User with id {sendingUserId} could not be found");
            }

            var userSession = (await _guestSessionRepository.GetItemsAsync(x => x.UserId == sendingUserId && x.ProjectAccessCode == accessCode)).FirstOrDefault();
            if (userSession == null)
            {
                throw new NotFoundException($"User guest session with userId {sendingUserId} and project access code {accessCode} could not be found");
            }

            var project = (await _projectApi.GetProjectByAccessCodeAsync(accessCode)).Payload;
            if (project == null)
            {
                throw new NotFoundException($"Project with access code {accessCode} could not be found");
            }

            var projectOwner = (await _userApi.GetUserAsync(project.OwnerId.GetValueOrDefault())).Payload;
            if (projectOwner == null)
            {
                throw new NotFoundException($"User for project owner {project.OwnerId} could not be found");
            }

            if (userSession.EmailedHostDateTime != null)
            {
                throw new Exception($"User {sendingUser.Email} has already emailed the host {projectOwner.Email} once for this guest session {userSession.Id}");
            }

            var invite = (await _guestInviteRepository.GetItemsAsync(x => x.UserId == sendingUserId && x.ProjectAccessCode == accessCode)).FirstOrDefault();
            var sendTo = invite == null ? projectOwner.Email : invite.GuestEmail;

            if (!_emailUtility.SendHostEmail(sendTo, sendingUser.GetFullName(), sendingUser.FirstName, sendingUser.Email, project.Name))
            {
                throw new Exception($"Email from user {sendingUser.Email} to host {projectOwner.Email} could not be sent");
            }

            userSession.EmailedHostDateTime = DateTime.UtcNow;
            await _guestSessionRepository.UpdateItemAsync(userSession.Id, userSession);

            return new SendHostEmailResponse()
            {
                EmailSentDateTime = DateTime.UtcNow,
                SentBy = sendingUser.Email
            };
        }
    }
}