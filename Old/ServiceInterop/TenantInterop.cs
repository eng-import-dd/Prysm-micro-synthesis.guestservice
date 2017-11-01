using Synthesis.GuestService.ApiWrappers;
using Synthesis.Http.Microservice;
using Synthesis.Logging;

namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public class TenantInterop : BaseInterop, ITenantInterop
    {
        public TenantInterop(IServiceLocator serviceLocator, IMicroserviceHttpClient httpClient, ILoggerFactory loggerFactory)
            : base(httpClient, loggerFactory)
        {
            ServiceUrl = serviceLocator.TenantUrl;
        }
    }
}