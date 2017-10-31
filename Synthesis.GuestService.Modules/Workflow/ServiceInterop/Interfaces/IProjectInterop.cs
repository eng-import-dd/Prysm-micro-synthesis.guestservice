using System;
using System.Threading.Tasks;

namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public interface IProjectInterop
    {
        Task<Project> GetProjectByAccessCodeAsync(string projectAccessCode);
        Task<Project> GetProjectByIdAsync(Guid projectId);
    }
}