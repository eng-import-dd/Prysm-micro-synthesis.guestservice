using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Synthesis.DocumentStorage;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.Threading.Tasks;

namespace Synthesis.GuestService.Controllers
{
    public class GuestTenantController : IGuestTenantController
    {
        private readonly AsyncLazy<IRepository<GuestSession>> _guestSessionLazyAsync;
        private readonly AsyncLazy<IRepository<GuestInvite>> _guestInviteLazyAsync;

        public GuestTenantController(IRepositoryFactory repositoryFactory)
        {
            _guestSessionLazyAsync = new AsyncLazy<IRepository<GuestSession>>(() => repositoryFactory.CreateRepositoryAsync<GuestSession>());
            _guestInviteLazyAsync = new AsyncLazy<IRepository<GuestInvite>>(() => repositoryFactory.CreateRepositoryAsync<GuestInvite>());
        }

        public async Task<IEnumerable<Guid>> GetTenantIdsForUserAsync(Guid userId)
        {
            var sessionRepo = await _guestSessionLazyAsync;
            var inviteRepo = await _guestInviteLazyAsync;

            var sessionTenantIdsTask = sessionRepo.CreateItemQuery()
                .Where(session => session.UserId == userId)
                .Select(session => session.ProjectTenantId)
                .ToListAsync();

            var inviteTenantIds = await inviteRepo.CreateItemQuery()
                .Where(invite => invite.UserId == userId)
                .Select(invite => invite.ProjectTenantId)
                .ToListAsync();

            var sessionTenantIds = await sessionTenantIdsTask;

            sessionTenantIds.AddRange(inviteTenantIds);

            return sessionTenantIds.Distinct();
        }
    }
}