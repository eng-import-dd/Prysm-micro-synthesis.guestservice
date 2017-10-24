using System.Threading.Tasks;
using Synthesis.GuestService.Dao.Models;
using Synthesis.GuestService.Workflow.ServiceInterop.Responses;

namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public interface IProjectInterop
    {
        Task<Project> GetProjectByAccessCodeAsync(string projectAccessCode);
    }
}