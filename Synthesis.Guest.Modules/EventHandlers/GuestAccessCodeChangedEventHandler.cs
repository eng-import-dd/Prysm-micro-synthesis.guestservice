using System;
using Synthesis.EventBus;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.InternalApi.Constants;
using Synthesis.Logging;
using Synthesis.ProjectService.InternalApi.Models;

namespace Synthesis.GuestService.EventHandlers
{
    public class GuestAccessCodeChangedEventHandler : IEventHandler<GuestAccessCodeChanged>
    {
        private readonly ILogger _logger;
        private readonly IGuestSessionController _guestSessionController;

        public GuestAccessCodeChangedEventHandler(ILoggerFactory loggerFactory, IGuestSessionController guestSessionController)
        {
            _logger = loggerFactory.GetLogger(this);
            _guestSessionController = guestSessionController;
        }

        /// <inheritdoc />
        public async void HandleEvent(GuestAccessCodeChanged args)
        {
            try
            {
                await _guestSessionController.DeleteGuestSessionsForProjectAsync(args.ProjectId, true);
            }
            catch (Exception ex)
            {
                _logger.Error($"Guests could not be kicked from project upon {EventNames.GuestAccessCodeChanged} event for project: {args.ProjectId}", ex);
            }
        }
    }
}