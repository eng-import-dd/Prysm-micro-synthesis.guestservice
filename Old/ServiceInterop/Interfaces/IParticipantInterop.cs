using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public interface IParticipantInterop
    {
        Task<ParticipantInteropResponse> GetParticipantsByProjectId(Guid projectId);
    }
}