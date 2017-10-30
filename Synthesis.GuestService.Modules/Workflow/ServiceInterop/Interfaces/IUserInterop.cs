using System.Threading.Tasks;
using Synthesis.GuestService.Workflow.ServiceInterop.Responses;

namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public interface IUserInterop
    {
        // TODO: Implement these methods
        Task<User> GetUserAsync(string username);

        Task<string> GenerateRandomPassword(int bitSize);

        Task<bool> IsUniqueEmail(string email);

        Task<bool> IsUniqueUsername(string username);

        Task<ProvisionGuestUserReturnCode> ProvisionGuestUser(string firstName, string lastName, string email, string passwordHash, string passwordSalt, bool isIdpUser);
    }
}