using Synthesis.GuestService.Dao.Models;
using System;
using System.Threading.Tasks;

namespace Synthesis.GuestService.Workflow.Interfaces
{
    public interface IGuestInviteController
    {
        Task<GuestInvite> CreateGuestInviteAsync(GuestInvite model);

        Task<GuestInvite> GetGuestInviteAsync(Guid guestInviteId);

        Task<GuestInvite> UpdateGuestInviteAsync(GuestInvite model);
    }
}
