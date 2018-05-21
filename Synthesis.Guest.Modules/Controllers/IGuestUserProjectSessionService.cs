using System.Threading.Tasks;

namespace Synthesis.GuestService.Controllers
{
    public interface IGuestUserProjectSessionService
    {
        Task<GuestProjectState> GetGuestUserStateAsync();
        Task SetGuestUserStateAsync(GuestProjectState guestProjectState);
        Task<bool> IsGuestAsync();
    }
}