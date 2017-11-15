using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.GuestService.ApiWrappers.Responses;
using Synthesis.Http.Microservice;

namespace Synthesis.GuestService.ApiWrappers.Interfaces
{
    public interface IParticipantApiWrapper
    {
        Task<MicroserviceResponse<IEnumerable<ParticipantResponse>>> GetParticipantsByProjectIdAsync(Guid projectId);
    }
}