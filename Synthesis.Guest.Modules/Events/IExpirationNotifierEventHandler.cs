using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Synthesis.EventBus.Events;

namespace Synthesis.GuestService.Events
{
    public interface IExpirationNotifierEventHandler
    {
        void HandleTriggerKickGuestsFromProjectEvent(GuidEvent args);
    }
}
