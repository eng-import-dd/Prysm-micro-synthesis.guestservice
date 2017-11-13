using Synthesis.GuestService.ApiWrappers.Interfaces;
using Synthesis.GuestService.ApiWrappers.Responses;
using Synthesis.Http.Microservice;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Synthesis.GuestService.ApiWrappers
{
    public class ParticipantApiWrapper : BaseApiWrapper, IParticipantApiWrapper
    {
        public ParticipantApiWrapper(IMicroserviceHttpClient microserviceHttpClient, string serviceUrl)
            : base(microserviceHttpClient, serviceUrl)
        {
        }

        public async Task<MicroserviceResponse<IEnumerable<ParticipantResponse>>> GetParticipantsByProjectIdAsync(Guid projectId)
        {
            return await HttpClient.GetManyAsync<ParticipantResponse>($"{ServiceUrl}/v1/projects/{projectId}/participants");
        }
    }
}