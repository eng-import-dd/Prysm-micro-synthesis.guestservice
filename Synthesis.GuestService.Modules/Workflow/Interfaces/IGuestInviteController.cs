using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Synthesis.GuestService.Entity;
using Synthesis.GuestService.Results;

namespace Synthesis.GuestService.Workflow.Interfaces
{
    public interface IGuestInviteController
    {
        // -- "Create" Methods
        Task<GuestInvite> CreateGuestInvite(GuestInvite request);

        // -- "Read" Methods
        Task<GuestInvite> GetGuestInviteByIdAsync(Guid guestSessionId);

        Task<IEnumerable<GuestInvite>> GetGuestInvitesByEmailAsync(string guestEmail);
        Task<IEnumerable<GuestInvite>> GetGuestInvitesByProjectId(Guid projectId);

        // -- "Update" Methods
        Task<GuestInvite> UpdateGuestInviteAsync(GuestInvite request);

        // -- "Delete" Methods
        // No delete operations for GuestInvite at this time
    }
}