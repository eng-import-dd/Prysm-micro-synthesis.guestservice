using System;
using System.Threading.Tasks;
using Synthesis.GuestService.InternalApi.Models;

namespace Synthesis.GuestService.Controllers
{
    public interface IProjectGuestContextController
    {
        Task<CurrentProjectState> SetProjectGuestContextAsync(Guid projectId, string accessCode, Guid currentUserId, Guid? currentUserTenantId);
        Task PromoteGuestUserToProjectMember(Guid userIdToPromote, Guid projectId, Guid currentUserId, Guid? currentUserTenantId);
    }
}