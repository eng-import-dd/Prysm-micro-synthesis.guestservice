using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Nancy;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.InternalApi.Constants;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Modules;
using Synthesis.PolicyEvaluator;

namespace Synthesis.GuestService.Modules
{
    public sealed class GuestTenantModule : SynthesisModule
    {
        private readonly IGuestTenantController _tenantController;

        public GuestTenantModule(
            IMetadataRegistry metadataRegistry,
            IPolicyEvaluator policyEvaluator,
            IGuestTenantController tenantController,
            ILoggerFactory loggerFactory)
            : base(GuestServiceBootstrapper.ServiceNameShort, metadataRegistry, policyEvaluator, loggerFactory)
        {
            // Init DI
            _tenantController = tenantController;

            // initialize routes
            CreateRoute("GetTenantIdsForGuest", HttpMethod.Get, "guesttenantids", GetTenantIdsForGuestAsync)
                .Description("Retrieves the list of tenantids for a guest user")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError)
                .ResponseFormat(new List<Guid> { Guid.Empty });
        }

        private async Task<object> GetTenantIdsForGuestAsync(dynamic input)
        {
            await RequiresAccess()
                .ExecuteAsync(CancellationToken.None);

            try
            {
                return await _tenantController.GetTenantIdsForUserAsync(PrincipalId);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create guestInvite resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateGuestInvite);
            }
        }
    }
}