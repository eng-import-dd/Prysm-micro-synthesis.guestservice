using System;
using Synthesis.EventBus;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Workflow.Controllers;
using Synthesis.Logging;

namespace Synthesis.GuestService.Events
{
    public class ResetGuestAccessCodeHandler : IEventHandler<GuidEvent>
    {
        private readonly ILogger _logger;
        private readonly IGuestSessionController _guestSessionController;

        public ResetGuestAccessCodeHandler(ILoggerFactory loggerFactory, IGuestSessionController guestSessionController)
        {
            _logger = loggerFactory.Get(new LogTopic(GetType().FullName));
            _guestSessionController = guestSessionController;
        }

        public async void HandleEvent(GuidEvent projectId)
        {
            try
            {
                await _guestSessionController.DeleteGuestSessionsForProjectAsync(projectId.Value, true);
            }
            catch (Exception ex)
            {
                _logger.Error($"Guests could not be kicked from project upon {EventNames.GuestAccessCodeUpdated} event for project: {projectId.Value}", ex);
            }
        }
    }
}