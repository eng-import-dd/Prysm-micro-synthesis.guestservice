using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation.Results;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Email;
using Synthesis.GuestService.Exceptions;
using Synthesis.GuestService.Extensions;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.Validators;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.InternalApi.Api;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.ProjectService.InternalApi.Api;
using Synthesis.ProjectService.InternalApi.Models;
using Synthesis.Serialization;

namespace Synthesis.GuestService.Controllers
{
    /// <summary>
    ///     Represents a controller for GuestInvite resources.
    /// </summary>
    /// <seealso cref="IGuestInviteController" />
    public class GuestInviteController : IGuestInviteController
    {
        private readonly IEventService _eventService;
        private readonly IRepository<GuestInvite> _guestInviteRepository;
        private readonly ILogger _logger;
        private readonly IValidatorLocator _validatorLocator;
        private readonly IProjectApi _projectApi;
        private readonly IUserApi _userApi;
        private readonly IEmailSendingService _emailSendingService;
        private readonly IObjectSerializer _serializer;

        /// <summary>
        ///     Initializes a new instance of the <see cref="GuestInviteController" /> class.
        /// </summary>
        /// <param name="userApi">The user api.</param>
        /// <param name="emailSendingService">The email sending service.</param>
        /// <param name="projectApi">The project API.</param>
        /// <param name="repositoryFactory">The repository factory.</param>
        /// <param name="validatorLocator">The validator locator.</param>
        /// <param name="eventService">The event service.</param>
        /// <param name="loggerFactory">The logger.</param>
        /// <param name="serializer">The serializer.</param>
        public GuestInviteController(
            IUserApi userApi,
            IProjectApi projectApi,
            IEmailSendingService emailSendingService,
            IRepositoryFactory repositoryFactory,
            IValidatorLocator validatorLocator,
            IEventService eventService,
            ILoggerFactory loggerFactory,
            IObjectSerializer serializer)
        {
            try
            {
                _guestInviteRepository = repositoryFactory.CreateRepository<GuestInvite>();
            }
            catch (Exception)
            {
                // supressing the repository exceptions for initial testing
            }

            _userApi = userApi;
            _emailSendingService = emailSendingService;
            _projectApi = projectApi;
            _validatorLocator = validatorLocator;
            _eventService = eventService;
            _logger = loggerFactory.GetLogger(this);
            _serializer = serializer;
        }

        public async Task<GuestInvite> CreateGuestInviteAsync(GuestInvite model)
        {
            var validationResult = _validatorLocator.Validate<GuestInviteValidator>(model);
            if (!validationResult.IsValid)
            {
                throw new ValidationFailedException(validationResult.Errors);
            }

            // Get dependent resources
            var project = await GetProjectAsync(model.ProjectId);
            var invitedByUser = await GetUserAsync(model.InvitedBy);
            var accessCode = await GetGuestAccessCodeAsync(project);

            model.Id = model.Id == Guid.Empty ? Guid.NewGuid() : model.Id;
            model.CreatedDateTime = DateTime.UtcNow;
            model.ProjectAccessCode = accessCode;

            var result = await _guestInviteRepository.CreateItemAsync(model);

            _eventService.Publish(EventNames.GuestInviteCreated, result);

            // Send an invite email to the guest
            var emailResult = await _emailSendingService.SendGuestInviteEmailAsync(project.Name, accessCode, model.GuestEmail, invitedByUser.FirstName, invitedByUser.LastName);
            if (!emailResult.IsSuccess())
            {
                _logger.Error($"Sending guest invite email failed. Reason={emailResult.ReasonPhrase} Error={_serializer.Serialize(emailResult.ErrorResponse)}");
            }

            return result;
        }

        private async Task<Project> GetProjectAsync(Guid guid)
        {
            var projectResult = await _projectApi.GetProjectByIdAsync(guid);
            if (!projectResult.IsSuccess() || projectResult.Payload == null)
            {
                throw new GetProjectException($"Could not get the project for Id={guid}, Reason={projectResult.ReasonPhrase} Error={_serializer.Serialize(projectResult.ErrorResponse)}");
            }

            return projectResult.Payload;
        }

        private async Task<User> GetUserAsync(Guid guid)
        {
            var userResult = await _userApi.GetUserAsync(guid);
            if (!userResult.IsSuccess() || userResult.Payload == null)
            {
                throw new GetUserException($"Could not get the user for Id={guid}, Reason={userResult.ReasonPhrase} Error={_serializer.Serialize(userResult.ErrorResponse)}");
            }

            return userResult.Payload;
        }

        private async Task<string> GetGuestAccessCodeAsync(Project project)
        {
            if (!string.IsNullOrEmpty(project.GuestAccessCode))
            {
                return project.GuestAccessCode;
            }

            var codeResult = await _projectApi.ResetGuestAccessCodeAsync(project.Id);
            if (!codeResult.IsSuccess() || codeResult.Payload == null)
            {
                throw new ResetAccessCodeException($"Could not reset the project access code for project with Id={project.Id}, Reason={codeResult.ReasonPhrase} Error={_serializer.Serialize(codeResult.ErrorResponse)}");
            }

            return codeResult.Payload;
        }

        public async Task<GuestInvite> GetGuestInviteAsync(Guid id)
        {
            var validationResult = _validatorLocator.Validate<GuestInviteIdValidator>(id);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the resource id while attempting to retrieve a GuestInvite resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var result = await _guestInviteRepository.GetItemAsync(id);
            if (result != null)
            {
                return result;
            }

            _logger.Error($"A GuestInvite resource could not be found for id {id}");
            throw new NotFoundException("GuestInvite could not be found");
        }

        public async Task<IEnumerable<GuestInvite>> GetGuestInvitesByProjectIdAsync(Guid projectId)
        {
            var validationResult = _validatorLocator.Validate<ProjectIdValidator>(projectId);
            if (!validationResult.IsValid)
            {
                _logger.Error("Failed to validate the projectId while attempting to retrieve GuestInvite resources.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            var result = await _guestInviteRepository.GetItemsAsync(x => x.ProjectId == projectId);
            if (result != null)
            {
                return result;
            }

            _logger.Error($"GuestInvite resources could not be found for projectId {projectId}");
            throw new NotFoundException("GuestInvites could not be found");
        }

        public async Task<GuestInvite> UpdateGuestInviteAsync(GuestInvite guestInviteModel)
        {
            var guestInviteIdValidationResult = _validatorLocator.Validate<GuestInviteIdValidator>(guestInviteModel.Id);
            var guestInviteValidationResult = _validatorLocator.Validate<GuestInviteValidator>(guestInviteModel);
            var errors = new List<ValidationFailure>();

            if (!guestInviteIdValidationResult.IsValid)
            {
                errors.AddRange(guestInviteIdValidationResult.Errors);
            }

            if (!guestInviteValidationResult.IsValid)
            {
                errors.AddRange(guestInviteValidationResult.Errors);
            }

            if (errors.Any())
            {
                _logger.Error("Failed to validate the resource id and/or resource while attempting to update a GuestInvite resource.");
                throw new ValidationFailedException(errors);
            }

            var result = await _guestInviteRepository.UpdateItemAsync(guestInviteModel.Id, guestInviteModel);

            _eventService.Publish(EventNames.GuestInviteUpdated, result);

            return result;
        }
    }
}