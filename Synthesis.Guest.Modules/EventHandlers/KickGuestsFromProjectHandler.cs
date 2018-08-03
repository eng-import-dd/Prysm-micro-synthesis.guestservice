using System;
using System.Threading.Tasks;
using Synthesis.EventBus;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.Retry;
using Synthesis.Logging;
using Synthesis.ProjectService.InternalApi.Models;
using Microsoft.Practices.TransientFaultHandling;
using Synthesis.Cache;
using Synthesis.ExpirationNotifierService.InternalApi.Services;
using Synthesis.GuestService.Enumerations;
using Synthesis.GuestService.Utilities.Interfaces;

namespace Synthesis.GuestService.EventHandlers
{
    public class KickGuestsFromProjectHandler : IEventHandler<GuidEvent>
    {
        private readonly ILogger _logger;
        private readonly IGuestSessionController _guestSessionController;
        private readonly ICacheSelector _cacheSelector;

        private readonly TimeSpan _retryKickingGuestsAfterFailureTimeSpan = TimeSpan.FromMinutes(5);

        public KickGuestsFromProjectHandler(
            ILoggerFactory loggerFactory,
            IGuestSessionController guestSessionController,
            ICacheSelector cacheSelector)
        {
            _logger = loggerFactory.GetLogger(this);
            _guestSessionController = guestSessionController;
            _cacheSelector = cacheSelector;
        }

        /// <inheritdoc />
        public async void HandleEvent(GuidEvent args)
        {
            try
            {
                var retryPolicy = new RetryPolicy<KickGuestsExceptionDetectionStrategy>(1, TimeSpan.Zero);
                retryPolicy.Retrying += (s, e) => _logger.Warning($"An Exception was thrown while kicking guests from {nameof(Project)}.Id={args.Value}  Retrying.");

                await retryPolicy.ExecuteAsync(() => KickGuestsInternal(args.Value));
            }
            catch (Exception ex)
            {
                _logger.Error($"Errors occurred while kicking guests for {nameof(Project)}.Id={args.Value}.", ex);

                _cacheSelector[CacheConnection.ExpirationNotifier].ItemSet(KeyResolver.ExpKickGuestsFromProject(args.Value), "Value not used", _retryKickingGuestsAfterFailureTimeSpan);
            }
        }

        private async Task KickGuestsInternal(Guid projectId)
        {
            await _guestSessionController.DeleteGuestSessionsForProjectAsync(projectId, true);
        }
    }
}
