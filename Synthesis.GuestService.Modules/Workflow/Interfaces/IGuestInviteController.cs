using Synthesis.GuestService.Dao.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Synthesis.GuestService.Workflow.Interfaces
{
    public interface IGuestInviteController
    {
        Task<GuestInvite> CreateGuestInviteAsync(GuestInvite model);

        Task<GuestInvite> GetGuestInviteAsync(Guid guestInviteId);

        Task<IEnumerable<GuestInvite>> GetGuestInvitesByProjectIdAsync(Guid projectId);

        Task<GuestInvite> UpdateGuestInviteAsync(GuestInvite model);
    }
}
