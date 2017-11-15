using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.GuestService.Models;

namespace Synthesis.GuestService.Controllers
{
    public interface IGuestInviteController
    {
        Task<GuestInvite> CreateGuestInviteAsync(GuestInvite model);

        Task<GuestInvite> GetGuestInviteAsync(Guid guestInviteId);

        Task<IEnumerable<GuestInvite>> GetGuestInvitesByProjectIdAsync(Guid projectId);

        Task<GuestInvite> UpdateGuestInviteAsync(GuestInvite model);
    }
}
