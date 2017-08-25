using Nancy;
using Nancy.ModelBinding;
using Nancy.Security;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Dao.Models;
using Synthesis.GuestService.Workflow.Interfaces;
using System;
using System.Threading.Tasks;

namespace Synthesis.GuestService.Modules
{
    public sealed class GuestSessionModule : NancyModule
    {
        private readonly IGuestSessionController _guestSessionController;
        private readonly IMetadataRegistry _metadataRegistry;
        private readonly ILogger _logger;

        public GuestSessionModule(
            IMetadataRegistry metadataRegistry,
            IGuestSessionController guestSessionController,
            ILogger logger)
        {
            // Init DI
            _metadataRegistry = metadataRegistry;
            _guestSessionController = guestSessionController;
            _logger = logger;

            this.RequiresAuthentication();

            // Initialize documentation
            SetupRouteMetadata();

            // CRUD routes
            Post(BaseRoutes.GuestSession, CreateGuestSessionAsync, null, "CreateGuestSession");
            Post(BaseRoutes.GuestSessionLegacy, CreateGuestSessionAsync, null, "CreateGuestSessionLegacy");

            Get(BaseRoutes.GuestSession + "/{id:guid}", GetGuestSessionAsync, null, "GetGuestSession");
            Get(BaseRoutes.GuestSessionLegacy + "/{id:guid}", GetGuestSessionAsync, null, "GetGuestSessionLegacy");

            Put(BaseRoutes.GuestSession + "/{id:guid}", UpdateGuestSessionAsync, null, "UpdateGuestSession");
            Put(BaseRoutes.GuestSessionLegacy + "/{id:guid}", UpdateGuestSessionAsync, null, "UpdateGuestSessionLegacy");

            OnError += (ctx, ex) =>
            {
                _logger.Error($"Unhandled exception while executing route {ctx.Request.Path}", ex);
                return Response.InternalServerError(ex.Message);
            };
        }

        private void SetupRouteMetadata()
        {
            _metadataRegistry.SetRouteMetadata("CreateGuestSession", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Create a new GuestSession",
                Description = "Create a specific GuestSession resource."
            });

            _metadataRegistry.SetRouteMetadata("GetGuestSession", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Get GuestSession",
                Description = "Retrieve a specific GuestSession resource."
            });

            _metadataRegistry.SetRouteMetadata("UpdateGuestSession", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Update GuestSession",
                Description = "Update a specific GuestSession resource."
            });
        }

        private async Task<object> CreateGuestSessionAsync(dynamic input)
        {
            GuestSession newGuestSession;
            try
            {
                newGuestSession = this.Bind<GuestSession>();
            }
            catch (Exception ex)
            {
                _logger.Warning("Binding failed while attempting to create a GuestSession resource", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                return await _guestSessionController.CreateGuestSessionAsync(newGuestSession);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to create guestSession resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateGuestSession);
            }
        }

        private async Task<object> GetGuestSessionAsync(dynamic input)
        {
            Guid id = input.id;

            try
            {
                return await _guestSessionController.GetGuestSessionAsync(id);
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.NotFoundGuestSession);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to get guestSession with id {id} due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetGuestSession);
            }
        }

        private async Task<object> UpdateGuestSessionAsync(dynamic input)
        {
            Guid guestSessionId;
            GuestSession guestSessionModel;

            try
            {
                guestSessionId = input.id;
                guestSessionModel = this.Bind<GuestSession>();
            }
            catch (Exception ex)
            {
                _logger.Warning("Binding failed while attempting to update a GuestSession resource.", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                return await _guestSessionController.UpdateGuestSessionAsync(guestSessionId, guestSessionModel);
            }
            catch (Exception ex)
            {
                _logger.Error("Unhandled exception encountered while attempting to update a GuestSession resource", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateGuestSession);
            }
        }
    }
}
