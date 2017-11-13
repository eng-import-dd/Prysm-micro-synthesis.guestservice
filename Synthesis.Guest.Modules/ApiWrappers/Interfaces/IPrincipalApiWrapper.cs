using System.Threading.Tasks;
using Synthesis.GuestService.ApiWrappers.Requests;
using Synthesis.GuestService.ApiWrappers.Responses;
using Synthesis.Http.Microservice;

namespace Synthesis.GuestService.ApiWrappers.Interfaces
{
    public interface IPrincipalApiWrapper
    {
        Task<MicroserviceResponse<UserResponse>> GetUserAsync(UserRequest request);

        Task<MicroserviceResponse<UserResponse>> ProvisionGuestUserAsync(UserRequest request);
    }
}