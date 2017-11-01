using System;
using System.Threading.Tasks;
using Synthesis.Http.Microservice;

namespace Synthesis.GuestService.Workflow.ApiWrappers
{
    public interface IProjectApiWrapper
    {
        Task<MicroserviceResponse<ProjectResponse>> GetProjectByAccessCodeAsync(string projectAccessCode);
        Task<MicroserviceResponse<ProjectResponse>> GetProjectByIdAsync(Guid projectId);
    }
}