using Synthesis.Http.Microservice;
using Synthesis.Logging;

namespace Synthesis.GuestService.Workflow.ApiWrappers
{
    public abstract class BaseApiWrapper
    {
        protected readonly IMicroserviceHttpClient HttpClient;
        protected readonly ILogger Logger;

        protected BaseApiWrapper(IMicroserviceHttpClient httpClient, ILoggerFactory loggerFactory)
        {
            HttpClient = httpClient;
            Logger = loggerFactory.GetLogger(GetType().FullName);
        }

        protected string ServiceUrl { get; set; }

        protected static bool IsSuccess(MicroserviceResponse response)
        {
            var code = response.ResponseCode;
            return (int)code >= 200
                   && (int)code <= 299;
        }
    }
}