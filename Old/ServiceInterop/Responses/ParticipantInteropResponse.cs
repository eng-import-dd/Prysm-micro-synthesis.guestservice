using System.Collections.Generic;

namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public class ParticipantInteropResponse : BaseInteropResponse
    {
        public List<Participant> ParticipantList { get; set; }
    }
}