using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.Http.Microservice;
using Synthesis.Logging;

namespace Synthesis.GuestService.Workflow.ApiWrappers
{
    public class ParticipantApiWrapper : BaseApiWrapper, IParticipantApiWrapper
    {
        public ParticipantApiWrapper(IServiceLocator serviceLocator, IMicroserviceHttpClient microserviceHttpClient, ILoggerFactory loggerFactory)
            : base(microserviceHttpClient, loggerFactory)
        {
            ServiceUrl = serviceLocator.ProjectUrl;
        }

        public async Task<MicroserviceResponse<IEnumerable<ParticipantResponse>>> GetParticipantsByProjectIdAsync(Guid projectId)
        {
            return await HttpClient.GetManyAsync<ParticipantResponse>($"{ServiceUrl}/v1/projects/{projectId}/participants");
        }
    }
}