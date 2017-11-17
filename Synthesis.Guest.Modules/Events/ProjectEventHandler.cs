using System;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.Models;
using Synthesis.Logging;

namespace Synthesis.GuestService.Events
{
    public class ProjectEventHandler : IProjectEventHandler
    {
        private readonly ILogger _logger;
        private readonly IGuestSessionController _guestSessionController;
        private readonly IProjectLobbyStateController _projectLobbyStateController;

        public ProjectEventHandler(ILoggerFactory loggerFactory,
            IGuestSessionController guestSessionController,
            IProjectLobbyStateController projectLobbyStateController)
        {
            _logger = loggerFactory.GetLogger(this);
            _guestSessionController = guestSessionController;
            _projectLobbyStateController = projectLobbyStateController;
        }

        /// <inheritdoc />
        public async void HandleGuestAccessCodeChangedEvent(GuidEvent args)
        {
            try
            {
                await _guestSessionController.DeleteGuestSessionsForProjectAsync(args.Value, true);
            }
            catch (Exception ex)
            {
                _logger.Error($"Guests could not be kicked from project upon {EventNames.GuestAccessCodeChanged} event for project: {args.Value}", ex);
            }
        }

        /// <inheritdoc />
        public async void HandleProjectCreatedEvent(Project project)
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

        /// <inheritdoc />
        public async void HandleProjectDeletedEvent(GuidEvent args)
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