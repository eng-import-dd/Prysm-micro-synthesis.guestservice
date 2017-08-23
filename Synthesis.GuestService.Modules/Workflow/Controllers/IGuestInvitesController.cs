using Synthesis.GuestService.Dao.Models;
using System;
using System.Threading.Tasks;

namespace Synthesis.GuestService.Workflow.Controllers
{
    public interface IGuestInvitesController
    {
        Task<GuestInvite> CreateGuestInviteAsync(GuestInvite model);

        Task<GuestInvite> GetGuestInviteAsync(Guid guestInviteId);

        Task<GuestInvite> UpdateGuestInviteAsync(Guid guestInviteId, GuestInvite model);
    }
}
