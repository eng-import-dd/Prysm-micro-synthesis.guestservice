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
        private const string ProjectGroup = "Group:Project:{0}";

        public ParticipantApiWrapper(IMicroserviceHttpClientResolver microserviceHttpClient, string serviceUrl)
            : base(microserviceHttpClient, serviceUrl)
        {
        }

        public async Task<MicroserviceResponse<IEnumerable<ParticipantResponse>>> GetParticipantsByProjectIdAsync(Guid projectId)
        {
            return await HttpClient.GetManyAsync<ParticipantResponse>($"{ServiceUrl}/v1/participants?group={string.Format(ProjectGroup, projectId)}");
        }
    }
}