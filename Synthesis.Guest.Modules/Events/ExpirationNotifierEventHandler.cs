using System;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.Controllers;
using Synthesis.Logging;

namespace Synthesis.GuestService.Events
{
    public class ExpirationNotifierEventHandler : IExpirationNotifierEventHandler
    {
        private readonly ILogger _logger;
        private readonly IGuestSessionController _guestSessionController;

        public ExpirationNotifierEventHandler(ILoggerFactory loggerFactory,
            IGuestSessionController guestSessionController)
        {
            _logger = loggerFactory.GetLogger(this);
            _guestSessionController = guestSessionController;
        }

        /// <inheritdoc />
        public async void HandleTriggerKickGuestsFromProjectEvent(GuidEvent args)
        {
            try
            {
                await _guestSessionController.DeleteGuestSessionsForProjectAsync(args.Value, true);
            }
            catch (Exception ex)
            {
                _logger.Error($"Could not kick guests for project {args.Value}", ex);
            }
        }
    }
}
