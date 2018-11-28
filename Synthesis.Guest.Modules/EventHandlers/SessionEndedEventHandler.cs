using System;
using Synthesis.EventBus;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.InternalApi.Enums;
using Synthesis.Logging;
using Synthesis.ParticipantService.InternalApi.Models;

namespace Synthesis.GuestService.EventHandlers
{
    public class SessionEndedEventHandler : IEventHandler<SessionEnded>
    {
        private readonly IGuestSessionController _guestSessionController;
        private readonly ILogger _logger;

        public SessionEndedEventHandler(
            IGuestSessionController guestSessionController,
            ILoggerFactory loggerFactory)
        {
            _guestSessionController = guestSessionController;
            _logger = loggerFactory.GetLogger(this);
        }

        public async void HandleEvent(SessionEnded payload)
        {
            try
            {
                var guestSession = await _guestSessionController.GetGuestSessionBySessionIdAsync(payload.SessionId);
                if (guestSession == null)
                {
                    return;
                }

                if (guestSession.GuestSessionState == GuestState.Ended)
                {
                    return;
                }

                guestSession.GuestSessionState = GuestState.Ended;
                await _guestSessionController.UpdateGuestSessionAsync(guestSession, guestSession.UserId);
            }
            catch (Exception ex)
            {
                _logger.Error($"Unexpected exception while ending the guest session for session ID {payload.SessionId}", ex);
            }
        }
    }
}