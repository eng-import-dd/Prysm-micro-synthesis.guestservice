using Synthesis.GuestService.ApiWrappers.Interfaces;
using Synthesis.GuestService.ApiWrappers.Responses;
using Synthesis.Http.Microservice;
using System;
using System.Threading.Tasks;
using Synthesis.Configuration;
using Synthesis.Http.Microservice.Api;

namespace Synthesis.GuestService.ApiWrappers
{
    public class SettingsApiWrapper : MicroserviceApi, ISettingsApiWrapper
    {
        public SettingsApiWrapper(IMicroserviceHttpClientResolver microserviceHttpClientResolver, IAppSettingsReader appSettingsReader)
            : base(microserviceHttpClientResolver, appSettingsReader, "SynthesisCloud.Url")
        {
        }

        public async Task<MicroserviceResponse<SettingsResponse>> GetSettingsAsync(Guid userId)
        {
            return await HttpClient.GetAsync<SettingsResponse>($"{ServiceUrl}/v1/settings/user/{userId}");
        }
    }
}