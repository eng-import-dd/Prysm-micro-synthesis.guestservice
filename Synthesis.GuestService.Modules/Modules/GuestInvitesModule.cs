using Nancy;
using Nancy.ModelBinding;
using Nancy.Security;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Dao.Models;
using Synthesis.GuestService.Workflow.Controllers;
using System;
using System.Threading.Tasks;

namespace Synthesis.GuestService.Modules
{
    public sealed class GuestInvitesModule : NancyModule
    {
        private readonly IGuestInvitesController _guestInviteController;
        private readonly IMetadataRegistry _metadataRegistry;
        private readonly ILogger _logger;

        public GuestInvitesModule(
            IMetadataRegistry metadataRegistry,
            IGuestInvitesController guestInviteController,
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
            Post("/v1/guestInvites", CreateGuestInviteAsync, null, "CreateGuestInvite");
            Post("/api/v1/guestInvites", CreateGuestInviteAsync, null, "CreateGuestInviteLegacy");

            Get("/v1/guestInvites/{id:guid}", GetGuestInviteAsync, null, "GetGuestInvite");
            Get("/api/v1/guestInvites/{id:guid}", GetGuestInviteAsync, null, "GetGuestInviteLegacy");

            Put("/v1/guestInvites/{id:guid}", UpdateGuestInviteAsync, null, "UpdateGuestInvite");
            Put("/api/v1/guestInvites/{id:guid}", UpdateGuestInviteAsync, null, "UpdateGuestInviteLegacy");

            Delete("/v1/guestInvites/{id:guid}", DeleteGuestInviteAsync, null, "DeleteGuestInvite");
            Delete("/api/v1/guestInvites/{id:guid}", DeleteGuestInviteAsync, null, "DeleteGuestInviteLegacy");

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
                Description = ""
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

            _metadataRegistry.SetRouteMetadata("DeleteGuestInvite", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.NoContent, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = "Delete GuestInvite",
                Description = "Delete a specific GuestInvite resource."
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
                return Response.BadRequestBindingException();
            }

            try
            {
                return await _guestInviteController.CreateGuestInviteAsync(newGuestInvite);
            }
            catch (ValidationFailedException ex)
            {
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
                return Response.NotFound(ResponseReasons.NotFoundGuestInvite);
            }
            catch (ValidationFailedException ex)
            {
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
                return Response.BadRequestBindingException();
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

        private async Task<object> DeleteGuestInviteAsync(dynamic input)
        {
            Guid guestInviteId = input.id;

            try
            {
                await _guestInviteController.DeleteGuestInviteAsync(guestInviteId);

                return new Response
                {
                    StatusCode = HttpStatusCode.NoContent,
                    ReasonPhrase = "Resource has been deleted"
                };
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                _logger.Error("Unhandled exception encountered while attempting to delete a GuestInvite resource", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorDeleteGuestInvite);
            }
        }
    }
}
