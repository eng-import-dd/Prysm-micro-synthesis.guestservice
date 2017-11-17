using Synthesis.EventBus.Events;
using Synthesis.GuestService.Models;

namespace Synthesis.GuestService.Events
{
    public interface IProjectEventHandler
    {
        void HandleGuestAccessCodeChangedEvent(GuidEvent args);
        void HandleProjectCreatedEvent(Project project);
        void HandleProjectDeletedEvent(GuidEvent args);
    }
}
