using System;
using Synthesis.EventBus;
using Synthesis.GuestService.Controllers;
using Synthesis.Logging;
using Synthesis.ProjectService.InternalApi.Models;

namespace Synthesis.GuestService.EventHandlers
{
    public class ProjectCreatedEventHandler : IEventHandler<Project>
    {
        private readonly ILogger _logger;
        private readonly IProjectLobbyStateController _projectLobbyStateController;

        public ProjectCreatedEventHandler(ILoggerFactory loggerFactory, IProjectLobbyStateController projectLobbyStateController)
        {
            _logger = loggerFactory.GetLogger(this);
            _projectLobbyStateController = projectLobbyStateController;
        }

        /// <inheritdoc />
        public async void HandleEvent(Project project)
        {
            try
            {
                await _projectLobbyStateController.CreateProjectLobbyStateAsync(project.Id);
            }
            catch (Exception e)
            {
                _logger.Error($"An error occurred creating lobby state for project: {project.Id}", e);
            }
        }
    }
}