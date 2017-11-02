using Synthesis.EventBus;

namespace Synthesis.GuestService.Constants
{
    internal static class EventNames
    {
        public const string GuestInviteCreated = "GuestInviteCreated";
        public const string GuestInviteUpdated = "GuestInviteUpdated";
        public const string GuestSessionCreated = "GuestSessionCreated";
        public const string GuestSessionUpdated = "GuestSessionUpdated";
        public const string GuestResetCodeUpdated = "GuestResetCodeUpdated";
    }

    public class EventSubscriber
    {
        public EventSubscriber(IEventService eventService, IGuestEventHandler eventHandler)
        {
            const string eventNamespace = EventNames.GuestResetCodeUpdated;
            eventService.Subscribe<ResetAccessCodeRequest>(eventNamespace, CobrowserEventNames.CobrowserStarted, eventHandler.HandleCobrowserStartedEvent);
        }
    }
}
