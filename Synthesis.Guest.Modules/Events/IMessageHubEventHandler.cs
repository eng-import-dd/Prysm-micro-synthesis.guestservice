using Synthesis.EventBus.Events;

namespace Synthesis.GuestService.Events
{
    public interface IMessageHubEventHandler
    {
        void HandleTriggerRecalculateProjectLobbyStateEvent(GuidEvent args);
    }
}
