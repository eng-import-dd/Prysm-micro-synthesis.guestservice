using Synthesis.EventBus;
using Synthesis.EventBus.Events;
using Synthesis.ExpirationNotifierService.InternalApi.Models;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.InternalApi.Constants;
using Synthesis.ProjectService.InternalApi.Models;
using ProjectEventNames = Synthesis.ProjectService.InternalApi.Constants.EventNames;

namespace Synthesis.GuestService.EventHandlers
{
    public class EventSubscriber
    {
        public EventSubscriber(IEventHandlerLocator eventHandlerLocator)
        {
            // project events
            eventHandlerLocator.SubscribeEventHandler<GuestAccessCodeChangedEventHandler, GuestAccessCodeChanged>(EventNamespaces.ProjectService, EventNames.GuestAccessCodeChanged);
            eventHandlerLocator.SubscribeEventHandler<GuestModeToggledEventHandler, GuestModeToggledEvent>(EventNamespaces.ProjectService, ProjectEventNames.GuestModeToggled);
            eventHandlerLocator.SubscribeEventHandler<ProjectCreatedEventHandler, Project>(EventNamespaces.ProjectService, ProjectEventNames.ProjectCreated);

            // message hub events
            eventHandlerLocator.SubscribeEventHandler<RecalculateProjectLobbyStateHandler, GuidEvent>(EventNamespaces.MessageHubService, EventNames.TriggerRecalculateProjectLobbyState);

            // expiration notifier events
            eventHandlerLocator.SubscribeEventHandler<KickGuestsFromProjectHandler, KickGuestsFromProjectRequest>(EventNamespaces.ExpirationNotifier, EventNames.TriggerKickGuestsFromProject);
        }
    }
}