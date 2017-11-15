using System;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.Controllers;
using Synthesis.Logging;

namespace Synthesis.GuestService.Events
{
    public class MessageHubEventHandler : IMessageHubEventHandler
    {
        private readonly ILogger _logger;
        private readonly IProjectLobbyStateController _projectLobbyStateController;

        public MessageHubEventHandler(ILoggerFactory loggerFactory,
            IProjectLobbyStateController projectLobbyStateController)
        {
            _logger = loggerFactory.GetLogger(this);
            _projectLobbyStateController = projectLobbyStateController;
        }

        /// <inheritdoc />
        public void HandleTriggerRecalculateProjectLobbyStateEvent(GuidEvent args)
        {
            try
            {
                _projectLobbyStateController.RecalculateProjectLobbyStateAsync(args.Value);
            }
            catch (Exception e)
            {
                _logger.Error($"An error occurred updating lobby state for project: {args.Value}", e);
            }
        }
    }
}
