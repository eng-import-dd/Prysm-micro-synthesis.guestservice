using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.GuestService.ApiWrappers.Requests;
using Synthesis.GuestService.ApiWrappers.Responses;
using Synthesis.GuestService.Models;
using Synthesis.GuestService.Requests;
using Synthesis.GuestService.Responses;

namespace Synthesis.GuestService.Controllers
{
    public interface IGuestSessionController
    {
        Task<GuestSession> CreateGuestSessionAsync(GuestSession model);
        Task<GuestCreationResponse> CreateGuestAsync(GuestCreationRequest model);
        Task<GuestSession> GetGuestSessionAsync(Guid guestSessionId);
        Task<IEnumerable<GuestSession>> GetGuestSessionsByProjectIdAsync(Guid projectId);
        Task<GuestSession> UpdateGuestSessionAsync(GuestSession model);
        Task<ProjectStatus> GetProjectStatusAsync(Guid projectId);
        Task<GuestVerificationEmailResponse> SendVerificationEmailAsync(GuestVerificationEmailRequest email);
        Task<GuestVerificationResponse> VerifyGuestAsync(string username, string projectAccessCode);
        Task DeleteGuestSessionsForProjectAsync(Guid projectId, bool onlyKickGuestsInProject);
    }
}