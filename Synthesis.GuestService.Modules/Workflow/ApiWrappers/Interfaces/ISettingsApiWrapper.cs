using System;
using System.Threading.Tasks;
using Synthesis.Http.Microservice;

namespace Synthesis.GuestService.Workflow.ApiWrappers
{
    public interface ISettingsApiWrapper
    {
        Task<MicroserviceResponse<SettingsResponse>> GetSettingsAsync(Guid projectAccountId);
    }
}