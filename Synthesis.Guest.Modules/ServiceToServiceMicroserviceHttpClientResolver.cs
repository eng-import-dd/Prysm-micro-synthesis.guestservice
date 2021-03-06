using Synthesis.Http.Microservice;

namespace Synthesis.GuestService
{
    public class ServiceToServiceMicroserviceHttpClientResolver : IMicroserviceHttpClientResolver
    {
        private readonly ServiceToServiceClient _serviceToServiceClient;

        public ServiceToServiceMicroserviceHttpClientResolver(ServiceToServiceClient serviceToServiceClient)
        {
            _serviceToServiceClient = serviceToServiceClient;
        }

        /// <inheritdoc />
        public IMicroserviceHttpClient Resolve()
        {
            return _serviceToServiceClient;
        }
    }
}