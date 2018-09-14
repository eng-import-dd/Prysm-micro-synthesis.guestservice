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
        /// <summary>
        /// Creates a guest session.
        /// </summary>
        /// <remarks>Exposed as public method only for unit testing. Should not be called by any external consumers of this interface.
        ///  External consumers should instead call <see cref="IProjectGuestContextController.SetProjectGuestContextAsync"/> to create a guest session.</remarks>
        /// <param name="model">The property values to assign to the created guest session</param>
        /// <returns></returns>
        Task<GuestSession> CreateGuestSessionAsync(GuestSession model);
        Task<GuestSession> GetGuestSessionAsync(Guid guestSessionId);
        Task<IEnumerable<GuestSession>> GetMostRecentValidGuestSessionsByProjectIdAsync(Guid projectId);
        Task<IEnumerable<GuestSession>> GetValidGuestSessionsByProjectIdForCurrentUserAsync(Guid projectId, Guid userId);
        Task<IEnumerable<GuestSession>> GetGuestSessionsByProjectIdForUserAsync(Guid projectId, Guid userId);
        Task<GuestSession> UpdateGuestSessionAsync(GuestSession model, Guid principalId);
        Task<GuestVerificationResponse> VerifyGuestAsync(GuestVerificationRequest request, Guid? guestTenantId);
        Task DeleteGuestSessionsForProjectAsync(Guid projectId, Guid initiateUserId, bool onlyKickGuestsInProject);
        Task<SendHostEmailResponse> EmailHostAsync(string accessCode, Guid sendingUserId);
        Task<UpdateGuestSessionStateResponse> UpdateGuestSessionStateAsync(UpdateGuestSessionStateRequest guestSessionRequest, Guid principalId);
    }
}