using System;
using System.Threading.Tasks;
using Synthesis.Http.Microservice;
using Synthesis.Logging;

namespace Synthesis.GuestService.Workflow.ApiWrappers
{
    public class SettingsApiWrapper : BaseApiWrapper, ISettingsApiWrapper
    {
        public SettingsApiWrapper(IServiceLocator serviceLocator, IMicroserviceHttpClient microserviceHttpClient, ILoggerFactory loggerFactory)
            : base(microserviceHttpClient, loggerFactory)
        {
            ServiceUrl = serviceLocator.ProjectUrl;
        }

        public async Task<MicroserviceResponse<SettingsResponse>> GetSettingsAsync(Guid userId)
        {
            return await HttpClient.GetAsync<SettingsResponse>($"{ServiceUrl}/v1/settings/user/{userId}");
        }
    }
}