using System;
using System.Collections.Generic;
using System.Linq;
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

            var guestInvites = new List<GuestInvite>();

            try
            {
                var guestInvitesEnumerable = await _guestInviteController.GetValidGuestInvitesByProjectIdAsync(args.ProjectId);
                guestInvites = guestInvitesEnumerable.ToList();
                // TODO: Now, we need to delete all of these GuestInvites from the CosmosDB Guest/GuestInvite collection. There is a delete in CreateGuestInviteAsync in GuestInviteController.cs
                // TODO: Will also need to implement GetValidGuestSessionsByProjectIdAsync() to get a list of GuestSessions to delete. GetAvailableGuestCountAsync in GuestSessionController
            }
            catch (Exception ex)
            {
                _logger.Error($"GuestInvites could not be retrieved for projectId {args.ProjectId}", ex);
            }

        }
    }
}