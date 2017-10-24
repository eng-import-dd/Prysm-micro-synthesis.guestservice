using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.GuestService.Dao.Models;
using Synthesis.GuestService.Responses;
using Synthesis.GuestService.Workflow.ServiceInterop.Responses;

namespace Synthesis.GuestService.Workflow.Interfaces
{
    public interface IGuestSessionController
    {
        Task<GuestSession> CreateGuestSessionAsync(GuestSession model);
        Task<GuestSession> GetGuestSessionAsync(Guid guestSessionId);
        Task<IEnumerable<GuestSession>> GetGuestSessionsByProjectIdAsync(Guid projectId);
        Task<GuestSession> UpdateGuestSessionAsync(GuestSession model);
        Task<GuestVerificationResponse> VerifyGuestAsync(string username, string projectAccessCode);
        Task<Guest> CreateGuestAsync(Guest guest);
        Task<ProjectStatus> GetProjectStatusAsync(Guid projectId);
        Task<Project> ResetAccessCodeAsync(Guid projectId);
        Task<GuestVerificationEmail> SendVerificationEmailAsync(GuestVerificationEmail email);
    }
}