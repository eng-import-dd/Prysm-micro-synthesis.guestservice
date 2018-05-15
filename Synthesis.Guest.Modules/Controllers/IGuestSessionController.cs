using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.InternalApi.Requests;
using Synthesis.GuestService.InternalApi.Responses;

namespace Synthesis.GuestService.Controllers
{
    public interface IGuestSessionController
    {
        Task<GuestSession> CreateGuestSessionAsync(GuestSession model);
        Task<GuestSession> GetGuestSessionAsync(Guid guestSessionId);
        Task<IEnumerable<GuestSession>> GetGuestSessionsByProjectIdAsync(Guid projectId);
        Task<GuestSession> UpdateGuestSessionAsync(GuestSession model);
        Task<GuestVerificationResponse> VerifyGuestAsync(string username, string projectAccessCode);
        Task DeleteGuestSessionsForProjectAsync(Guid projectId, bool onlyKickGuestsInProject);
        Task<SendHostEmailResponse> EmailHostAsync(string accessCode, Guid sendingUserId);
    }
}