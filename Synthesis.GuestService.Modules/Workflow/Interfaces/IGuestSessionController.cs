using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.GuestService.Dao.Models;
using Synthesis.GuestService.Workflow.ServiceInterop.Responses;

namespace Synthesis.GuestService.Workflow.Interfaces
{
    public interface IGuestSessionController
    {
        Task<GuestSession> CreateGuestSessionAsync(GuestSession model);
        Task<GuestSession> GetGuestSessionAsync(Guid guestSessionId);
        Task<IEnumerable<GuestSession>> GetGuestSessionsByProjectIdAsync(Guid projectId);
        Task<GuestSession> UpdateGuestSessionAsync(GuestSession model);
        Task<ProjectStatus> GetProjectStatusAsync(Guid projectId);
    }
}