using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.GuestService.Dao.Models;
using Synthesis.GuestService.Requests;
using Synthesis.GuestService.Responses;
using Synthesis.GuestService.Workflow.ServiceInterop;

namespace Synthesis.GuestService.Workflow.Controllers
{
    public interface IGuestSessionController
    {
        Task<GuestSession> CreateGuestSessionAsync(GuestSession model);
        Task<GuestCreationResponse> CreateGuestAsync(GuestCreationRequest model);
        Task<GuestSession> GetGuestSessionAsync(Guid guestSessionId);
        Task<IEnumerable<GuestSession>> GetGuestSessionsByProjectIdAsync(Guid projectId);
        Task<GuestSession> UpdateGuestSessionAsync(GuestSession model);
        Task<ProjectStatus> GetProjectStatusAsync(Guid projectId);
        Task<GuestVerificationEmail> SendVerificationEmailAsync(GuestVerificationEmail email);
        Task<GuestVerificationResponse> VerifyGuestAsync(string username, string projectAccessCode);
    }
}