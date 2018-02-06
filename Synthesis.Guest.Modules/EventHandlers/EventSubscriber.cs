using Synthesis.EventBus;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Models;

namespace Synthesis.GuestService.EventHandlers
{
    public class EventSubscriber
    {
        public EventSubscriber(IEventHandlerLocator eventHandlerLocator)
        {
            // project events
            eventHandlerLocator.SubscribeEventHandler<GuestAccessCodeChangedEventHandler, GuidEvent>(EventNamespaces.ProjectService, EventNames.GuestAccessCodeChanged);
            eventHandlerLocator.SubscribeEventHandler<ProjectCreatedEventHandler, Project>(EventNamespaces.ProjectService, EventNames.ProjectCreated);
            eventHandlerLocator.SubscribeEventHandler<ProjectDeletedEventHandler, GuidEvent>(EventNamespaces.ProjectService, EventNames.ProjectDeleted);

            // message hub events
            eventHandlerLocator.SubscribeEventHandler<RecalculateProjectLobbyStateHandler, GuidEvent>(EventNamespaces.MessageHubService, EventNames.TriggerRecalculateProjectLobbyState);

            // expiration notifier events
            eventHandlerLocator.SubscribeEventHandler<KickGuestsFromProjectHandler, GuidEvent>(EventNamespaces.ExpirationNotifier, EventNames.TriggerKickGuestsFromProject);
        }
    }
}