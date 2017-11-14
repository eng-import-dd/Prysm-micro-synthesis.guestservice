using Synthesis.GuestService.Models;
using System;
using System.Threading.Tasks;

namespace Synthesis.GuestService.Controllers
{
    public interface IProjectLobbyStateController
    {
        Task CreateProjectLobbyStateAsync(Guid projectId);
        Task RecalculateProjectLobbyStateAsync(Guid projectId);
        Task<ProjectLobbyState> GetProjectLobbyStateAsync(Guid projectId);
        Task DeleteProjectLobbyStateAsync(Guid projectId);
    }
}
