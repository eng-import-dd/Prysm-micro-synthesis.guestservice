using Synthesis.Http.Microservice;
using Synthesis.Logging;

namespace Synthesis.GuestService.ApiWrappers
{
    public abstract class BaseApiWrapper
    {
        protected readonly IMicroserviceHttpClient HttpClient;
        protected readonly string ServiceUrl;

        protected BaseApiWrapper(IMicroserviceHttpClient httpClient, string serviceUrl)
        {
            HttpClient = httpClient;
            ServiceUrl = serviceUrl;
        }

        protected static bool IsSuccess(MicroserviceResponse response)
        {
            var code = response.ResponseCode;
            return (int)code >= 200
                   && (int)code <= 299;
        }
    }
}