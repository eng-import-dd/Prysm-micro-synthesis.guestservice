using System.Threading.Tasks;
using Synthesis.Http.Microservice;
using Synthesis.Logging;

namespace Synthesis.GuestService.Workflow.ApiWrappers
{
    public class PrincipalApiWrapperWrapper : BaseApiWrapper, IPrincipalApiWrapper
    {
        public PrincipalApiWrapperWrapper(IServiceLocator serviceLocator, IMicroserviceHttpClient microserviceHttpClient, ILoggerFactory loggerFactory)
            : base(microserviceHttpClient, loggerFactory)
        {
            ServiceUrl = serviceLocator.ProjectUrl;
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