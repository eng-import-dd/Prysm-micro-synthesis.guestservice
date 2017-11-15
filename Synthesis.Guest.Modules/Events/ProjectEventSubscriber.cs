using Synthesis.EventBus;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Models;

namespace Synthesis.GuestService.Events
{
    public class ProjectEventSubscriber
    {
        public ProjectEventSubscriber(IEventService eventService,
            IProjectEventHandler projectEventHandler)
        {
            eventService.Subscribe<GuidEvent>(EventNamespaces.ProjectService, EventNames.GuestAccessCodeChanged, projectEventHandler.HandleGuestAccessCodeChangedEvent);
            eventService.Subscribe<Project>(EventNamespaces.ProjectService, EventNames.ProjectCreated, projectEventHandler.HandleProjectCreatedEvent);
            eventService.Subscribe<GuidEvent>(EventNamespaces.ProjectService, EventNames.ProjectDeleted, projectEventHandler.HandleProjectDeletedEvent);
        }
    }
}