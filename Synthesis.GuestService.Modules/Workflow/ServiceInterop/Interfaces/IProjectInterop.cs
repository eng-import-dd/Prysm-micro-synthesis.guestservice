using System;
using System.Threading.Tasks;
using Synthesis.GuestService.Workflow.ServiceInterop.Responses;

namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public interface IProjectInterop
    {
        // TODO: Implement these methods
        Task<Project> GetProjectByAccessCodeAsync(string projectAccessCode);
        Task<Project> GetProjectByIdAsync(Guid projectId);
    }
}