using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.Http.Microservice;

namespace Synthesis.GuestService.Workflow.ApiWrappers
{
    public interface IParticipantApiWrapper
    {
        Task<MicroserviceResponse<IEnumerable<ParticipantResponse>>> GetParticipantsByProjectIdAsync(Guid projectId);
    }
}