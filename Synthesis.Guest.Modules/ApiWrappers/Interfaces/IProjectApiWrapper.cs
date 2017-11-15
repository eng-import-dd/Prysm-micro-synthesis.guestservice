using System;
using System.Threading.Tasks;
using Synthesis.GuestService.ApiWrappers.Responses;
using Synthesis.Http.Microservice;

namespace Synthesis.GuestService.ApiWrappers.Interfaces
{
    public interface IProjectApiWrapper
    {
        Task<MicroserviceResponse<ProjectResponse>> GetProjectByAccessCodeAsync(string projectAccessCode);
        Task<MicroserviceResponse<ProjectResponse>> GetProjectByIdAsync(Guid projectId);
    }
}