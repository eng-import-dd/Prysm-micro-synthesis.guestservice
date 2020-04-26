using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;
using Synthesis.EventBus;
using Synthesis.ExpirationNotifierService.InternalApi.Models;
using Synthesis.ExpirationNotifierService.InternalApi.Services;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.Retry;
using Synthesis.Logging;
using System;
using System.Threading.Tasks;

namespace Synthesis.GuestService.EventHandlers
{
    public class KickGuestsFromProjectHandler : IEventHandler<KickGuestsFromProjectRequest>
    {
        private readonly ILogger _logger;
        private readonly IGuestSessionController _guestSessionController;
        private readonly INotificationService _cacheNotificationService;

        public KickGuestsFromProjectHandler(
            INotificationService cacheNotificationService,
            ILoggerFactory loggerFactory,
            IGuestSessionController guestSessionController)
        {
            _cacheNotificationService = cacheNotificationService;
            _logger = loggerFactory.GetLogger(this);
            _guestSessionController = guestSessionController;
        }

        /// <inheritdoc />
        public async void HandleEvent(KickGuestsFromProjectRequest request)
        {
            try
            {
                var retryPolicy = new RetryPolicy<KickGuestsExceptionDetectionStrategy>(1, TimeSpan.Zero);
                retryPolicy.Retrying += (s, e) => _logger.Warning($"An Exception was thrown while kicking guests from ProjectId={request.ProjectId}.  Retrying.");

                await retryPolicy.ExecuteAsync(() => KickGuestsInternal(request));
            }
            catch (Exception ex)
            {
                _logger.Error($"Errors occurred while kicking guests for ProjectId={request.ProjectId}.", ex);

                await _cacheNotificationService.RetryScheduleKickGuestsNotificationAsync(request);
            }
        }

        private async Task KickGuestsInternal(KickGuestsFromProjectRequest request)
        {
            await _guestSessionController.EndGuestSessionsForProjectAsync(request.ProjectId, request.PrincipalId, true);
        }
    }
}
