using Synthesis.GuestService.Dao.Models;
using System;
using System.Threading.Tasks;

namespace Synthesis.GuestService.Workflow.Interfaces
{
    public interface IGuestSessionController
    {
        Task<GuestSession> CreateGuestSessionAsync(GuestSession model);

        Task<GuestSession> GetGuestSessionAsync(Guid guestSessionId);

        Task<GuestSession> UpdateGuestSessionAsync(GuestSession model);
    }
}
