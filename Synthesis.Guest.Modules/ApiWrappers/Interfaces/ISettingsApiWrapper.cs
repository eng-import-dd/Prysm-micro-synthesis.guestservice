using System;
using System.Threading.Tasks;
using Synthesis.GuestService.ApiWrappers.Responses;
using Synthesis.Http.Microservice;

namespace Synthesis.GuestService.ApiWrappers.Interfaces
{
    public interface ISettingsApiWrapper
    {
        Task<MicroserviceResponse<SettingsResponse>> GetSettingsAsync(Guid projectAccountId);
    }
}