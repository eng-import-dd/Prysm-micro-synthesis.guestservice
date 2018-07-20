using System;
using System.Threading.Tasks;
using Synthesis.GuestService.InternalApi.Models;

namespace Synthesis.GuestService.Controllers
{
    public interface IProjectGuestContextController
    {
        Task<CurrentProjectState> SetProjectGuestContextAsync(Guid projectId, string accessCode, Guid currentUserId, Guid? currentUserTenantId);
    }
}