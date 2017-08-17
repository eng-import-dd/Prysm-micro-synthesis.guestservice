using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Synthesis.GuestService.Entity;
using Synthesis.GuestService.Results;

namespace Synthesis.GuestService.Workflow.Interfaces
{
    public interface IGuestSessionController
    {
        // -- "Create" Methods
        Task<GuestSession> CreateGuestSession(GuestSession request);

        // -- "Read" Methods
        Task<GuestSession> GetGuestSessionByIdAsync(Guid guestSessionId);
        Task<IEnumerable<GuestSession>> GetGuestSessionsByUserId(Guid userId);
        Task<IEnumerable<GuestSession>> GetGuestSessionsByProjectId(Guid projectId);

        // -- "Update" Methods
        Task<GuestSession> UpdateGuestSession(GuestSession request);

        // -- "Delete" Methods
        // No delete operations for GuestSession at this time
    }
}
