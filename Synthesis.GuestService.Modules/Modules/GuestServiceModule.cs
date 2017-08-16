
using System;
using Nancy;
using Nancy.Security;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Security;

namespace Synthesis.GuestService.Modules
{
    public class GuestServiceModule : NancyModule
    {
        public GuestServiceModule(IMetadataRegistry metadataRegistry) :
            base("/api/v1/guestservice")
        {
            this.RequiresAuthentication();
            // add some additional data for the documentation module
            metadataRegistry.SetRouteMetadata("Hello", new SynthesisRouteMetadata()
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Some informational message",
                Description = "Gets a synthesis user by id."
            });
            // create a health check endpoint
            Get("/hello", (_) =>
            {
                try
                {
                /*
                    this.RequiresClaims(
                        c => c.Type == SynthesisStatelessAuthenticationConfiguration.PERMISSION_CLAIM_TYPE && GetPermission(c.Value) == PermissionEnum.CanLoginToAdminPortal);
                */
                }
                catch (Exception)
                {
                    return HttpStatusCode.Unauthorized;
                }
                // TODO: do some kind of health check if it passes return OK, otherwise 500
                return new Response()
                {
                    StatusCode = HttpStatusCode.OK,
                    ReasonPhrase = "Hello World"
                };
            }, null, "Hello");
        }

        internal PermissionEnum GetPermission(string value)
        {
            return ((PermissionEnum)Int32.Parse(value));
        }
    }
}
