using Autofac;
using Synthesis.Http.Microservice;

namespace Synthesis.GuestService
{
    public class MicroserviceHttpClientResolver : IMicroserviceHttpClientResolver
    {
        private readonly ILifetimeScope _container;
        public MicroserviceHttpClientResolver(ILifetimeScope container)
        {
            _container = container;
        }

        /// <inheritdoc />
        public IMicroserviceHttpClient Resolve()
        {
            var canResolve = _container.TryResolve<IRequestHeaders>(out var _);
            return _container.ResolveKeyed<IMicroserviceHttpClient>(canResolve ? GuestServiceBootstrapper.AuthorizationPassThroughKey : GuestServiceBootstrapper.ServiceToServiceKey);
        }
    }
}
