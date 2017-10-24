using System.Threading.Tasks;
using Synthesis.GuestService.Workflow.ServiceInterop.Responses;

namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public interface IUserInterop
    {
        Task<User> GetUserAsync(string username);
    }
}