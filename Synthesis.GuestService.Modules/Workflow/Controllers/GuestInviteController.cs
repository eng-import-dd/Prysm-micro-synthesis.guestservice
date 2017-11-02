using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Dao.Models;
using Synthesis.GuestService.Validators;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Validation;

namespace Synthesis.GuestService.Workflow.Controllers
{
    /// <summary>
    ///     Represents a controller for GuestInvite resources.
    /// </summary>
    /// <seealso cref="IGuestInviteController" />
    public class GuestInviteController : IGuestInviteController
    {
        private readonly IEventService _eventService;
        private readonly IValidator _guestInviteIdValidator;
        private readonly IRepository<GuestInvite> _guestInviteRepository;
        private readonly IValidator _guestInviteValidator;
        private readonly ILogger _logger;
        private readonly IValidator _projectIdValidator;

        /// <summary>
        ///     Initializes a new instance of the <see cref="GuestInviteController" /> class.
        /// </summary>
        /// <param name="repositoryFactory">The repository factory.</param>
        /// <param name="validatorLocator">The validator locator.</param>
        /// <param name="eventService">The event service.</param>
        /// <param name="logger">The logger.</param>
        public GuestInviteController(
            IRepositoryFactory repositoryFactory,
            IValidatorLocator validatorLocator,
            IEventService eventService,
            ILogger logger)
        {
            try
            {
                _guestInviteRepository = repositoryFactory.CreateRepository<GuestInvite>();
            }
            catch (Exception)
            {
                // supressing the repository exceptions for initial testing
            }
            _guestInviteValidator = validatorLocator.GetValidator(typeof(GuestInviteValidator));
            _guestInviteIdValidator = validatorLocator.GetValidator(typeof(GuestInviteIdValidator));
            _projectIdValidator = validatorLocator.GetValidator(typeof(ProjectIdValidator));
            _eventService = eventService;
            _logger = logger;
        }

        public async Task<GuestInvite> CreateGuestInviteAsync(GuestInvite model)
        {
            var validationResult = await _guestInviteValidator.ValidateAsync(model);
            if (!validationResult.IsValid)
            {
                _logger.Error("Validation failed while attempting to create a GuestInvite resource.");
                throw new ValidationFailedException(validationResult.Errors);
            }

            model.Id = model.Id == Guid.Empty ? Guid.NewGuid() : model.Id;
            model.CreatedDateTime = DateTime.UtcNow;

            var result = await _guestInviteRepository.CreateItemAsync(model);

            _eventService.Publish(EventNames.GuestInviteCreated, result);

            return result;
        }

        public async Task<GuestInvite> GetGuestInviteAsync(Guid id)
        {
            var validationResult = await _guestInviteIdValidator.ValidateAsync(id);
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
            var validationResult = await _projectIdValidator.ValidateAsync(projectId);
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
            var guestInviteIdValidationResult = await _guestInviteIdValidator.ValidateAsync(guestInviteModel.Id);
            var guestInviteValidationResult = await _guestInviteValidator.ValidateAsync(guestInviteModel);
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