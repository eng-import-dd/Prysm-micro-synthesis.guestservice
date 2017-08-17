using Nancy;
using Nancy.Bootstrapper;
using Nancy.TinyIoc;
using Synthesis.KeyManager;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Authorization;
using Synthesis.Nancy.MicroService.Dao;
using Synthesis.GuestService.Workflow.Controllers;
using Synthesis.GuestService.Workflow.Interfaces;
using Synthesis.GuestService.Dao;

namespace Synthesis.GuestService
{
    public class GuestServiceBootstrapper : DefaultNancyBootstrapper
    {
        protected override void ConfigureApplicationContainer(TinyIoCContainer existingContainer)
        {
            base.ConfigureApplicationContainer(existingContainer);
            existingContainer.Register<ILoggingService, SimpleLoggingService>().AsSingleton();
            existingContainer.Register<IKeyManager, SimpleKeyManager>().AsSingleton();
            existingContainer.Register<ISynthesisMonolithicCloudDao, SynthesisMonolithicCloudDao>().AsSingleton();
            existingContainer.Register<IGuestInviteController, GuestInviteController>();
            existingContainer.Register<IRepositoryFactory, RepositoryFactory>();

            // Update this registration if you need to change the authorization implementation.
            existingContainer.Register<IStatelessAuthorization, SynthesisStatelessAuthorization>().AsSingleton();
        }

        protected override void ApplicationStartup(TinyIoCContainer container, IPipelines pipelines)
        {
            // Add the micro-service authorization logic to the Nancy pipeline.
            StatelessAuthorization.Enable(pipelines, container.Resolve<IStatelessAuthorization>());

            base.ApplicationStartup(container, pipelines);
        }
    }
}