using System;
using Synthesis.EventBus;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.InternalApi.Constants;
using Synthesis.GuestService.InternalApi.Enums;
using Synthesis.Logging;
using Synthesis.ParticipantService.InternalApi.Models;

namespace Synthesis.GuestService.EventHandlers
{
    public class SessionEndedEventHandler : IEventHandler<SessionEnded>
    {
        private readonly IGuestSessionController _guestSessionController;
        private readonly IProjectLobbyStateController _projectLobbyStateController;
        private readonly ILogger _logger;

        public SessionEndedEventHandler(
            IGuestSessionController guestSessionController,
            IProjectLobbyStateController projectLobbyStateController,
            ILoggerFactory loggerFactory)
        {
            _guestSessionController = guestSessionController;
            _projectLobbyStateController = projectLobbyStateController;
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

                // Recalc lobby state after guest session is ended.
                await _projectLobbyStateController.RecalculateProjectLobbyStateAsync(guestSession.ProjectId);
            }
            catch (Exception ex)
            {
                _logger.Error($"Unexpected exception while ending the guest session for session ID {payload.SessionId}", ex);
            }
        }
    }
}