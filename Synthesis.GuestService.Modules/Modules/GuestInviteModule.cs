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
    public sealed class GuestInviteModule : NancyModule
    {
        private readonly IGuestInviteController _guestInviteController;
        private readonly IMetadataRegistry _metadataRegistry;
        private readonly ILogger _logger;

        public GuestInviteModule(
            IMetadataRegistry metadataRegistry,
            IGuestInviteController guestInviteController,
            ILogger logger)
        {
            // Init DI
            _metadataRegistry = metadataRegistry;
            _guestInviteController = guestInviteController;
            _logger = logger;

            this.RequiresAuthentication();

            // Initialize documentation
            SetupRouteMetadata();

            // CRUD routes
            Post(BaseRoutes.GuestInvite, CreateGuestInviteAsync, null, "CreateGuestInvite");
            Post(BaseRoutes.GuestInviteLegacy, CreateGuestInviteAsync, null, "CreateGuestInviteLegacy");

            Get(BaseRoutes.GuestInvite + "/{id:guid}", GetGuestInviteAsync, null, "GetGuestInvite");
            Get(BaseRoutes.GuestInviteLegacy + "/{id:guid}", GetGuestInviteAsync, null, "GetGuestInviteLegacy");

            Put(BaseRoutes.GuestInvite + "/{id:guid}", UpdateGuestInviteAsync, null, "UpdateGuestInvite");
            Put(BaseRoutes.GuestInviteLegacy + "/{id:guid}", UpdateGuestInviteAsync, null, "UpdateGuestInviteLegacy");

            OnError += (ctx, ex) =>
            {
                _logger.Error($"Unhandled exception while executing route {ctx.Request.Path}", ex);
                return Response.InternalServerError(ex.Message);
            };
        }

        private void SetupRouteMetadata()
        {
            _metadataRegistry.SetRouteMetadata("CreateGuestInvite", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Create a new GuestInvite",
                Description = "Create a specific GuestInvite resource."
            });

            _metadataRegistry.SetRouteMetadata("GetGuestInvite", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Get GuestInvite",
                Description = "Retrieve a specific GuestInvite resource."
            });

            _metadataRegistry.SetRouteMetadata("UpdateGuestInvite", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Update GuestInvite",
                Description = "Update a specific GuestInvite resource."
            });
        }

        private async Task<object> CreateGuestInviteAsync(dynamic input)
        {
            GuestInvite newGuestInvite;
            try
            {
                newGuestInvite = this.Bind<GuestInvite>();
            }
            catch (Exception ex)
            {
                _logger.Warning("Binding failed while attempting to create a GuestInvite resource", ex);
                return Response.BadRequestBindingException(ResponseReasons.FailedToBindToRequest);
            }

            try
            {
                return await _guestInviteController.CreateGuestInviteAsync(newGuestInvite);
            }
            catch (ValidationFailedException ex)
            {
                _logger.Error("Validation failed while attempting to create a GuestInvite resource", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to create guestInvite resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateGuestInvite);
            }
        }

        private async Task<object> GetGuestInviteAsync(dynamic input)
        {
            Guid id = input.id;

            try
            {
                return await _guestInviteController.GetGuestInviteAsync(id);
            }
            catch (NotFoundException)
            {
                _logger.Warning($"GuestInvite with id {id} could not be found");
                return Response.NotFound(ResponseReasons.NotFoundGuestInvite);
            }
            catch (ValidationFailedException ex)
            {
                _logger.Error($"Validation failed for guestInvite with id {id} due to an error", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to get guestInvite with id {id} due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetGuestInvite);
            }
        }

        private async Task<object> UpdateGuestInviteAsync(dynamic input)
        {
            Guid guestInviteId;
            GuestInvite guestInviteModel;

            try
            {
                guestInviteId = input.id;
                guestInviteModel = this.Bind<GuestInvite>();
            }
            catch (Exception ex)
            {
                _logger.Warning("Binding failed while attempting to update a GuestInvite resource.", ex);
                return Response.BadRequestBindingException(ResponseReasons.FailedToBindToRequest);
            }

            try
            {
                return await _guestInviteController.UpdateGuestInviteAsync(guestInviteId, guestInviteModel);
            }
            catch (Exception ex)
            {
                _logger.Error("Unhandled exception encountered while attempting to update a GuestInvite resource", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateGuestInvite);
            }
        }
    }
}
