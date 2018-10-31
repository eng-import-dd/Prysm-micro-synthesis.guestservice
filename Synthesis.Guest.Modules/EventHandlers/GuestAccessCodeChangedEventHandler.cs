using System;
using System.Collections.Generic;
using Synthesis.EventBus;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.InternalApi.Constants;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.Logging;
using Synthesis.ProjectService.InternalApi.Models;

namespace Synthesis.GuestService.EventHandlers
{
    public class GuestAccessCodeChangedEventHandler : IEventHandler<GuestAccessCodeChanged>
    {
        private readonly ILogger _logger;
        private readonly IGuestSessionController _guestSessionController;
        private readonly IGuestInviteController _guestInviteController;

        public GuestAccessCodeChangedEventHandler(
            ILoggerFactory loggerFactory,
            IGuestSessionController guestSessionController,
            IGuestInviteController guestInviteController)
        {
            _logger = loggerFactory.GetLogger(this);
            _guestSessionController = guestSessionController;
            _guestInviteController = guestInviteController;
        }

        /// <inheritdoc />
        public async void HandleEvent(GuestAccessCodeChanged args)
        {
            try
            {
                await _guestSessionController.EndGuestSessionsForProjectAsync(args.ProjectId, args.UserId, false);
            }
            catch (Exception ex)
            {
                _logger.Error($"Guests could not be kicked from project upon {EventNames.GuestAccessCodeChanged} event for project: {args.ProjectId}", ex);
            }

            try
            {
                await _guestInviteController.DeleteGuestInvitesByProjectIdAsync(args.ProjectId, args.PreviousGuestAccessCode);
            }
            catch (Exception ex)
            {
                _logger.Error($"GuestInvites could not be deleted for projectId {args.ProjectId}", ex);
            }

        }
    }
}