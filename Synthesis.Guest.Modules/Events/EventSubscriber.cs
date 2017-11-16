using Synthesis.EventBus;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Models;

namespace Synthesis.GuestService.Events
{
    public class EventSubscriber
    {
        public EventSubscriber(IEventService eventService,
            IProjectEventHandler projectEventHandler,
            IMessageHubEventHandler messageHubEventHandler)
        {
            // project events
            eventService.Subscribe<GuidEvent>(EventNamespaces.ProjectService, EventNames.GuestAccessCodeChanged, projectEventHandler.HandleGuestAccessCodeChangedEvent);
            eventService.Subscribe<Project>(EventNamespaces.ProjectService, EventNames.ProjectCreated, projectEventHandler.HandleProjectCreatedEvent);
            eventService.Subscribe<GuidEvent>(EventNamespaces.ProjectService, EventNames.ProjectDeleted, projectEventHandler.HandleProjectDeletedEvent);

            // message hub events
            eventService.Subscribe<GuidEvent>(EventNamespaces.MessageHubService, EventNames.TriggerRecalculateProjectLobbyState, messageHubEventHandler.HandleTriggerRecalculateProjectLobbyStateEvent);

            // TODO - CU-403 - Subscribe to new KEN to kick guests out of project after last non guest leaves
            // https://prysminc.atlassian.net/browse/CU-403
        }
    }
}