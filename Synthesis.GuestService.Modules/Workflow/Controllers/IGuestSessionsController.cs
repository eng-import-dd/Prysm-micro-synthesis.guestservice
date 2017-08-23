using Synthesis.GuestService.Dao.Models;
using System;
using System.Threading.Tasks;

namespace Synthesis.GuestService.Workflow.Controllers
{
    public interface IGuestSessionsController
    {
        Task<GuestSession> CreateGuestSessionAsync(GuestSession model);

        Task<GuestSession> GetGuestSessionAsync(Guid guestSessionId);

        Task<GuestSession> UpdateGuestSessionAsync(Guid guestSessionId, GuestSession model);
    }
}
