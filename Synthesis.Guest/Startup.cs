using Microsoft.Owin.Cors;
using Microsoft.Owin.Extensions;
using Owin;
using Synthesis.GuestService.Owin;
using Synthesis.Owin.Security;
using Synthesis.Tracking.Web;

namespace Synthesis.GuestService
{
    public static class Startup
    {
        // This code configures Web API. The Startup class is specified as a type
        // parameter in the WebApp.Start method.
        public static void ConfigureApp(IAppBuilder app)
        {
            app.UseCors(CorsOptions.AllowAll);

            // Enables IoC for OwinMiddlware implementations. This method allows us to control
            // the order of our middleware.
            app.UseAutofacLifetimeScopeInjector(GuestServiceBootstrapper.RootContainer);

            app.UseMiddlewareFromContainer<GlobalExceptionHandlerMiddleware>();
            app.UseMiddlewareFromContainer<CorrelationScopeMiddleware>();

            // This middleware performs our authentication and populates the user principal.
            app.UseMiddlewareFromContainer<SynthesisAuthenticationMiddleware>();
            app.UseStageMarker(PipelineStage.Authenticate);

            app.UseNancy(options =>
            {
                options.Bootstrapper = new GuestServiceBootstrapper();
            });
        }
    }
}
