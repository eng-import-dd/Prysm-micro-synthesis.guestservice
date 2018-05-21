using System;
using System.Linq;
using System.Threading.Tasks;
using Synthesis.Cache;
using Synthesis.Http.Microservice;

namespace Synthesis.GuestService.Controllers
{
    public class GuestUserProjectSessionService : IGuestUserProjectSessionService
    {
        private readonly ICache _cache;
        private readonly string _userSessionId;

        public GuestUserProjectSessionService(ICache cache, IRequestHeaders requestHeaders)
        {
            _cache = cache;
            _userSessionId = requestHeaders["SessionIdString"].FirstOrDefault() ?? requestHeaders["SessionId"].FirstOrDefault();
        }

        public async Task<GuestProjectState> GetGuestUserStateAsync()
        {
            return await _cache.ItemGetAsync<GuestProjectState>(KeyResolver.GetGuestUserStateKey(_userSessionId));
        }

        public async Task SetGuestUserStateAsync(GuestProjectState guestProjectState)
        {
            await _cache.ItemSetAsync(KeyResolver.GetGuestUserStateKey(_userSessionId), guestProjectState, TimeSpan.FromHours(24));
        }

        public async Task<bool> IsGuestAsync()
        {
            var session = await GetGuestUserStateAsync();
            return session != null && session.ProjectId != Guid.Empty && session.TenantId != Guid.Empty;
        }
    }
}