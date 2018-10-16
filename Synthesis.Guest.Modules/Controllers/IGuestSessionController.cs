using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.InternalApi.Requests;
using Synthesis.GuestService.InternalApi.Responses;
using Synthesis.ProjectService.InternalApi.Models;

namespace Synthesis.GuestService.Controllers
{
    public interface IGuestSessionController
    {
        /// <summary>
        /// Creates a guest session.
        /// </summary>
        /// <remarks>Exposed as public method only for unit testing. Should not be called by any external consumers of this interface.
        ///  External consumers should instead call <see cref="IProjectGuestContextController.SetProjectGuestContextAsync"/> to create a guest session.</remarks>
        /// <param name="model">The property values to assign to the created guest session</param>
        /// <param name="principalId"></param>
        /// <param name="tenantId"></param>
        /// <returns></returns>
        Task<GuestSession> CreateGuestSessionAsync(GuestSession model, Guid principalId, Guid tenantId);
        Task<GuestSession> GetGuestSessionAsync(Guid guestSessionId);
        Task<IEnumerable<GuestSession>> GetMostRecentValidGuestSessionsByProjectIdAsync(Guid projectId);
        Task<IEnumerable<GuestSession>> GetValidGuestSessionsByProjectIdForCurrentUserAsync(Guid projectId, Guid userId);
        Task<IEnumerable<GuestSession>> GetGuestSessionsByProjectIdForUserAsync(Guid projectId, Guid userId);
        Task<GuestSession> UpdateGuestSessionAsync(GuestSession model, Guid principalId);
        Task<GuestVerificationResponse> VerifyGuestAsync(GuestVerificationRequest request, Guid? guestTenantId);
        Task<GuestVerificationResponse> VerifyGuestAsync(GuestVerificationRequest request, Project project, Guid? guestTenantId);
        Task EndGuestSessionsForProjectAsync(Guid projectId, Guid principalId, bool onlyKickGuestsInProject);
        Task DeleteGuestSessionAsync(Guid id);
        Task<SendHostEmailResponse> EmailHostAsync(string accessCode, Guid sendingUserId);
        Task<UpdateGuestSessionStateResponse> UpdateGuestSessionStateAsync(UpdateGuestSessionStateRequest guestSessionRequest, Guid principalId);
    }
}