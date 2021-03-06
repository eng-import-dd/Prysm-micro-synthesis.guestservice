using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.GuestService.InternalApi.Models;

namespace Synthesis.GuestService.Controllers
{
    public interface IGuestInviteController
    {
        Task<GuestInvite> CreateGuestInviteAsync(GuestInvite model);

        Task<GuestInvite> GetGuestInviteAsync(Guid guestInviteId);

        Task<IEnumerable<GuestInvite>> GetValidGuestInvitesByProjectIdAsync(Guid projectId);

        Task<IEnumerable<GuestInvite>> GetGuestInvitesForUserAsync(GetGuestInvitesRequest request);

        Task<GuestInvite> UpdateGuestInviteAsync(GuestInvite model);

        Task DeleteGuestInvitesByProjectIdAsync(Guid projectId, string previousGuestAccessCode);

        Task<bool> IsGuestRegistrationRequired(string email, string accessCode);
    }
}