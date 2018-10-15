using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Synthesis.GuestService.Controllers
{
    public interface IGuestTenantController
    {
        Task<IEnumerable<Guid>> GetTenantIdsForUserAsync(Guid guestInviteId);
    }
}