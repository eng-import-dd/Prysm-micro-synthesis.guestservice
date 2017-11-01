using System;
using System.Threading.Tasks;

namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public interface IProjectInterop
    {
        Task<ProjectInteropResponse> GetProjectByAccessCodeAsync(string projectAccessCode);
        Task<ProjectInteropResponse> GetProjectByIdAsync(Guid projectId);
    }
}