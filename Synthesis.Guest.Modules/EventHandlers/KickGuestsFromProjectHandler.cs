using System;
using System.Threading.Tasks;
using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;
using Synthesis.EventBus;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.Retry;
using Synthesis.Logging;
using Synthesis.ExpirationNotifierService.InternalApi.Services;

namespace Synthesis.GuestService.EventHandlers
{
    public class KickGuestsFromProjectHandler : IEventHandler<GuidEvent>
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
        public async void HandleEvent(GuidEvent args)
        {
            try
            {
                var retryPolicy = new RetryPolicy<KickGuestsExceptionDetectionStrategy>(1, TimeSpan.Zero);
                retryPolicy.Retrying += (s, e) => _logger.Warning($"An Exception was thrown while kicking guests from ProjectId={args.Value}.  Retrying.");

                await retryPolicy.ExecuteAsync(() => KickGuestsInternal(args.Value));
            }
            catch (Exception ex)
            {
                _logger.Error($"Errors occurred while kicking guests for ProjectId={args.Value}.", ex);

                await _cacheNotificationService.RetryScheduleKickGuestsNotificationAsync(args.Value);
            }
        }

        private async Task KickGuestsInternal(Guid projectId)
        {
            await _guestSessionController.DeleteGuestSessionsForProjectAsync(projectId, true);
        }
    }
}
