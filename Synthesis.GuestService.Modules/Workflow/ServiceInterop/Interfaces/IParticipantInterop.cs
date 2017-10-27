using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.GuestService.Workflow.ServiceInterop.Responses;

namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public interface IParticipantInterop
    {
        // TODO: Implement this method
        Task<List<Participant>> GetParticipantsByProjectId(Guid projectId);
    }
}