using Synthesis.EventBus;
using Synthesis.EventBus.Events;
using Synthesis.GuestService.Constants;

namespace Synthesis.GuestService.Events
{
    public class EventSubscriber
    {
        public EventSubscriber(IEventHandlerLocator eventHandlerLocator)
        {
            eventHandlerLocator.SubscribeEventHandler<ResetGuestAccessCodeHandler, GuidEvent>(EventNamespaces.GuestService, EventNames.GuestAccessCodeUpdated);
            eventHandlerLocator.SubscribeEventHandler<AllUsersHaveDepartedProjectHandler, GuidEvent>(EventNamespaces.GuestService, EventNames.AllUsersHaveDepartedProject);
        }
    }
}