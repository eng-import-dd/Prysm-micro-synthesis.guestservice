using Synthesis.GuestService.ApiWrappers.Interfaces;
using Synthesis.GuestService.ApiWrappers.Requests;
using Synthesis.GuestService.ApiWrappers.Responses;
using Synthesis.Http.Microservice;
using System.Threading.Tasks;

namespace Synthesis.GuestService.ApiWrappers
{
    public class PrincipalApiWrapper : BaseApiWrapper, IPrincipalApiWrapper
    {
        public PrincipalApiWrapper(IMicroserviceHttpClientResolver httpClient, string serviceUrl) : base(httpClient, serviceUrl)
        {
        }

        public async Task<MicroserviceResponse<UserResponse>> GetUserAsync(UserRequest request)
        {
            return await HttpClient.GetAsync<UserResponse>($"{ServiceUrl}/v1/users");
        }

        public async Task<MicroserviceResponse<UserResponse>> ProvisionGuestUserAsync(UserRequest request)
        {
            return await HttpClient.PostAsync<UserRequest, UserResponse>($"{ServiceUrl}/v1/users", request);
        }
    }
}