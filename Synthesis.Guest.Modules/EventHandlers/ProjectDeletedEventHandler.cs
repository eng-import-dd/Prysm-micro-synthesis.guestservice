using System;
using Synthesis.EventBus;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.Controllers;
using Synthesis.Logging;

namespace Synthesis.GuestService.EventHandlers
{
    public class ProjectDeletedEventHandler : IEventHandler<GuidEvent>
    {
        private readonly ILogger _logger;
        private readonly IProjectLobbyStateController _projectLobbyStateController;

        public ProjectDeletedEventHandler(ILoggerFactory loggerFactory, IProjectLobbyStateController projectLobbyStateController)
        {
            _logger = loggerFactory.GetLogger(this);
            _projectLobbyStateController = projectLobbyStateController;
        }

        /// <inheritdoc />
        public async void HandleEvent(GuidEvent args)
        {
            try
            {
                await _projectLobbyStateController.DeleteProjectLobbyStateAsync(args.Value);
            }
            catch (Exception e)
            {
                _logger.Error($"An error occurred creating lobby state for project: {args.Value}", e);
            }
        }
    }
}