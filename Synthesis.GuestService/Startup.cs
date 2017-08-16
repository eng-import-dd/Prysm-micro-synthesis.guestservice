using Owin;
using Synthesis.Nancy.MicroService.Tracing;
using Microsoft.Owin.Cors;
using Synthesis.GuestService.Modules;

namespace Synthesis.GuestService
{
    public static class Startup
    {
        // This code configures Web API. The Startup class is specified as a type
        // parameter in the WebApp.Start method.
        public static void ConfigureApp(IAppBuilder app)
        {
            app.UseCors(CorsOptions.AllowAll);
            app.Use(typeof(CorrelationTokenMiddleware));
            app.UseNancy(options =>
            {
                options.Bootstrapper = new GuestServiceBootstrapper();
            });
        }
    }
}