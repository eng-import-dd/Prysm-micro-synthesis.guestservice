using System;
using Synthesis.EventBus;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.Controllers;
using Synthesis.Logging;

namespace Synthesis.GuestService.EventHandlers
{
    public class KickGuestsFromProjectHandler : IEventHandler<GuidEvent>
    {
        private readonly ILogger _logger;
        private readonly IGuestSessionController _guestSessionController;

        public KickGuestsFromProjectHandler(ILoggerFactory loggerFactory,
            IGuestSessionController guestSessionController)
        {
            _logger = loggerFactory.GetLogger(this);
            _guestSessionController = guestSessionController;
        }

        /// <inheritdoc />
        public async void HandleEvent(GuidEvent args)
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
