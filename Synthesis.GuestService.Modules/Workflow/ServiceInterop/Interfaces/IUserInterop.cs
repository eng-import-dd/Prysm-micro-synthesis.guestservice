using System.Threading.Tasks;
using Synthesis.GuestService.Workflow.ServiceInterop.Responses;

namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public interface IUserInterop
    {
        // TODO: Implement this method
        Task<User> GetUserAsync(string username);
    }
}