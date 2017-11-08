using System.Threading.Tasks;
using Synthesis.Http.Microservice;

namespace Synthesis.GuestService.Workflow.ApiWrappers
{
    public interface IPrincipalApiWrapper
    {
        Task<MicroserviceResponse<UserResponse>> GetUserAsync(UserRequest request);

        Task<MicroserviceResponse<UserResponse>> ProvisionGuestUserAsync(UserRequest request);
    }
}