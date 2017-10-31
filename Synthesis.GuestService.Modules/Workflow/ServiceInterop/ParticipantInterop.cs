using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Synthesis.Http.Microservice;
using Synthesis.Logging;

namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public class ParticipantInterop : BaseInterop, IParticipantInterop
    {
        public ParticipantInterop(IServiceLocator serviceLocator, IMicroserviceHttpClient httpClient, ILoggerFactory loggerFactory)
            : base(httpClient, loggerFactory)
        {
            ServiceUrl = serviceLocator.ParticipantUrl;
        }

        public async Task<ParticipantInteropResponse> GetParticipantsByProjectId(Guid projectId)
        {
            try
            {
                var result = await HttpClient.GetManyAsync<Participant>($"{ServiceUrl}/v1/projects/{projectId}/participants");

                if (!IsSuccess(result))
                {
                    Logger.Warning($"Call to participant service failed to retrieve participants for projectId {projectId}");
                    return new ParticipantInteropResponse
                    {
                        ResponseCode = InteropResponseCode.Fail
                    };
                }

                if (result.Payload.Any())
                {
                    return new ParticipantInteropResponse
                    {
                        ParticipantList = result.Payload.ToList(),
                        ResponseCode = InteropResponseCode.Success
                    };
                }

                Logger.Warning($"No participant records found for projectId {projectId}");
                return new ParticipantInteropResponse
                {
                    ResponseCode = InteropResponseCode.NoRecordsReturned
                };
            }
            catch (Exception ex)
            {
                const string message = "Exception thrown calling the participant service to retrieve participants by projectId";
                Logger.Error(message, ex);
                return null;
            }
        }
    }
}