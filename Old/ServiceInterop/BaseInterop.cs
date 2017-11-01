using Synthesis.GuestService.Extensions;
using Synthesis.Http.Microservice;
using Synthesis.Logging;


namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public abstract class BaseInterop
    {
        protected readonly IMicroserviceHttpClient HttpClient;
        protected readonly ILogger Logger;

        protected BaseInterop(IMicroserviceHttpClient httpClient, ILoggerFactory loggerFactory)
        {
            HttpClient = httpClient;
            Logger = loggerFactory.GetLogger(this);
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
