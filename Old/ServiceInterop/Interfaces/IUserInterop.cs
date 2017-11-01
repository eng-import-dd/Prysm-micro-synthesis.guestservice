using System.Threading.Tasks;

namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public interface IUserInterop
    {
        Task<UserInteropResponse> GetUserAsync(string username);

        Task<UserInteropResponse> IsUniqueEmail(string email);

        Task<UserInteropResponse> IsUniqueUsername(string username);

        Task<UserInteropResponse> ProvisionGuestUser(User user);
    }
}