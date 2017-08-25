using FluentValidation;
using FluentValidation.Results;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Dao.Models;
using Synthesis.GuestService.Validators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Synthesis.Nancy.MicroService;

namespace Synthesis.GuestService.Workflow.Controllers
{
    /// <summary>
    /// Represents a controller for GuestSession resources.
    /// </summary>
    /// <seealso cref="Synthesis.GuestService.Workflow.Controllers.IGuestSessionsController" />
    public class GuestSessionsController : IGuestSessionsController
    {
        private readonly IRepository<GuestSession> _guestSessionRepository;
        private readonly IValidator _guestSessionValidator;
        private readonly IValidator _guestSessionIdValidator;
        private readonly IEventService _eventService;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="GuestSessionsController"/> class.
        /// </summary>
        /// <param name="repositoryFactory">The repository factory.</param>
        /// <param name="validatorLocator">The validator locator.</param>
        /// <param name="eventService">The event service.</param>
        /// <param name="logger">The logger.</param>
        public GuestSessionsController(
            IRepositoryFactory repositoryFactory,
            IValidatorLocator validatorLocator,
            IEventService eventService,
            ILogger logger)
        {
            try
            {
                _guestSessionRepository = repositoryFactory.CreateRepository<GuestSession>();
            }
            catch (Exception)
            {
                // supressing the repository exceptions for initial testing
            }
            _guestSessionValidator = validatorLocator.GetValidator(typeof(GuestSessionValidator));
            _guestSessionIdValidator = validatorLocator.GetValidator(typeof(GuestSessionIdValidator));
            _eventService = eventService;
            _logger = logger;
        }

        public async Task<GuestSession> CreateGuestSessionAsync(GuestSession model)
        {
            var validationResult = await _guestSessionValidator.ValidateAsync(model);
            if (!validationResult.IsValid)
            {
                _logger.Warning("Validation failed while attempting to create a GuestSession resource.");
                ValidationFailedException.Raise<GuestSession>(validationResult.Errors);
            }

            model.Id = model.Id == Guid.Empty ? Guid.NewGuid() : model.Id;
            model.CreatedDateTime = DateTime.UtcNow;
            var result = await _guestSessionRepository.CreateItemAsync(model);

            _eventService.Publish(EventNames.GuestSessionCreated, result);

            return result;
        }

        public async Task<GuestSession> GetGuestSessionAsync(Guid id)
        {
            var validationResult = await _guestSessionIdValidator.ValidateAsync(id);
            if (!validationResult.IsValid)
            {
                _logger.Warning("Failed to validate the resource id while attempting to retrieve a GuestSession resource.");
                ValidationFailedException.Raise<GuestSession>(validationResult.Errors);
            }

            var result = await _guestSessionRepository.GetItemAsync(id);

            if (result == null)
            {
                _logger.Warning($"A GuestSession resource could not be found for id {id}");
                NotFoundException.Raise();
            }

            return result;
        }

        public async Task<GuestSession> UpdateGuestSessionAsync(Guid guestSessionId, GuestSession guestSessionModel)
        {
            var guestSessionIdValidationResult = await _guestSessionIdValidator.ValidateAsync(guestSessionId);
            var guestSessionValidationResult = await _guestSessionValidator.ValidateAsync(guestSessionModel);
            var errors = new List<ValidationFailure>();

            if (!guestSessionIdValidationResult.IsValid)
            {
                errors.AddRange(guestSessionIdValidationResult.Errors);
            }

            if (!guestSessionValidationResult.IsValid)
            {
                errors.AddRange(guestSessionValidationResult.Errors);
            }

            if (errors.Any())
            {
                _logger.Warning("Failed to validate the resource id and/or resource while attempting to update a GuestSession resource.");
                ValidationFailedException.Raise<GuestSession>(errors);
            }

            try
            {
                return await _guestSessionRepository.UpdateItemAsync(guestSessionId, guestSessionModel);
            }
            catch (DocumentNotFoundException)
            {
                return null;
            }
        }
    }
}
