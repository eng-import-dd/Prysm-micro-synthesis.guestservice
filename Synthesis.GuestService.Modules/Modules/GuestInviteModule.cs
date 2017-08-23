using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Nancy;
using Nancy.ModelBinding;
using Nancy.Security;
using Newtonsoft.Json;
using Synthesis.GuestService.Constants;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Security;
using Synthesis.GuestService.Entity;
using Synthesis.GuestService.Exceptions;
using Synthesis.GuestService.Validators;
using Synthesis.GuestService.Workflow.Interfaces;
using Synthesis.GuestService.Workflow.Controllers;
using Synthesis.Nancy.MicroService.Extensions;
//using ILogger = Synthesis.Logging.ILogger;
//using LogLevel = Synthesis.Logging.LogLevel;

namespace Synthesis.GuestService.Modules
{
    public class GuestInviteModule : NancyModule
    {
        private readonly IMetadataRegistry _metadataRegistry;
        private readonly IGuestInviteController _guestInviteController;
        //private readonly ILogger _logger;

        #region Constructor

        public GuestInviteModule(IMetadataRegistry metadataRegistry, IGuestInviteController guestInviteController/*, ILoggingService logger*/) :
            base("/")
        {
            // -- Initialize Properties
            _metadataRegistry = metadataRegistry;
            _guestInviteController = guestInviteController;
            //_logger = logger;

            // -- Initialize Routes
            // Health
            SetupRoute_HealthCheck(metadataRegistry);

            // Create
            SetupRoute_CreateGuestInvite(metadataRegistry);

            // Read
            SetupRoute_GetGuestInviteById(metadataRegistry);
            SetupRoute_GetGuestInvitesByProjectId(metadataRegistry);
            SetupRoute_GetGuestInvitesbyEmail(metadataRegistry);

            // Update
            SetupRoute_UpdateGuestInvite(metadataRegistry);

            // Delete
            // No GuestInvite delete operations at this time

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

        #endregion

        #region HealthCheck Route Setup

        // -- Health Check Route
        private void SetupRoute_HealthCheck(IMetadataRegistry metadataRegistry)
        {

        }

        #endregion

        #region GuestInvite Route Setup
        private void SetupRoute_CreateGuestInvite(IMetadataRegistry metadataRegistry)
        {
            _metadataRegistry.SetRouteMetadata("CreateGuestInvite", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = JsonConvert.SerializeObject(new GuestInvite()),
                Description = "Creates a new GuestInvite."
            });

            Post(BaseRoutes.GuestInvites, CreateGuestInvite, null, "CreateGuestInvite");
            Post(BaseRoutes.GuestInvitesLegacy, CreateGuestInvite, null, "CreateGuestInviteLegacy");
        }
        private void SetupRoute_GetGuestInviteById(IMetadataRegistry metadataRegistry)
        {
            _metadataRegistry.SetRouteMetadata("GetGuestInviteById", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = JsonConvert.SerializeObject(new GuestInvite()),
                Description = "Gets a GuestInvite by passing in a GuestInviteId."
            });

            Get(BaseRoutes.GuestInvites + "{guestInviteId:guid}", GetGuestInviteById, null, "GetGuestInviteById");
            Get(BaseRoutes.GuestInvitesLegacy + "{guestInviteId:guid}", GetGuestInviteById, null, "GetGuestInviteByIdLegacy");
        }
        private void SetupRoute_GetGuestInvitesbyEmail(IMetadataRegistry metadataRegistry)
        {
            _metadataRegistry.SetRouteMetadata("GetGuestInviteByEmail", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = JsonConvert.SerializeObject(new GuestInvite()),
                Description = "Gets a list of GuestInvites by passing in a GuestEmail."
            });

            Get(BaseRoutes.GuestInvites + "{guestEmail:string}", GetGuestInvitesbyEmail, null, "GetGuestInvitesbyEmail");
            Get(BaseRoutes.GuestInvitesLegacy + "{guestEmail:string}", GetGuestInvitesbyEmail, null, "GetGuestInvitesbyEmailLegacy");
        }
        private void SetupRoute_GetGuestInvitesByProjectId(IMetadataRegistry metadataRegistry)
        {
            _metadataRegistry.SetRouteMetadata("GetGuestInviteByProjectId", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = JsonConvert.SerializeObject(new GuestInvite()),
                Description = "Gets a list of GuestInvites by passing in a ProjectId."
            });

            Get(BaseRoutes.GuestInvites + "{projectId:guid}", GetGuestInvitesByProjectId, null, "GetGuestInvitesByProjectId");
            Get(BaseRoutes.GuestInvitesLegacy + "{projectId:guid}", GetGuestInvitesByProjectId, null, "GetGuestInvitesByProjectIdLegacy");
        }
        private void SetupRoute_UpdateGuestInvite(IMetadataRegistry metadataRegistry)
        {
            _metadataRegistry.SetRouteMetadata("UpdateGuestInvite", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = JsonConvert.SerializeObject(new GuestInvite()),
                Description = "Updates a GuestInvites by passing in a GuestInvite object with the updated fields."
            });

            Put(BaseRoutes.GuestInvites, UpdateGuestInvite, null, "UpdateGuestInvite");
            Put(BaseRoutes.GuestInvitesLegacy, UpdateGuestInvite, null, "UpdateGuestInviteLegacy");
        }
        #endregion

        #region GuestInvite Route Methods
        private async Task<object> CreateGuestInvite(dynamic input)
        {
            GuestInvite newGuestInvite;
            try
            {
                newGuestInvite = this.Bind<GuestInvite>();
            }
            catch (Exception ex)
            {
                //_logger.LogMessage(LogLevel.Warning, "Binding failed during CreateGuestInvite", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                return await _guestInviteController.CreateGuestInvite(newGuestInvite);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                //_logger.LogMessage(LogLevel.Error, "CreateGuestInviteAsync threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreatingGuestInvite);
            }
        }
        private async Task<object> GetGuestInviteById(dynamic input)
        {
            Guid guestInviteId = input.guestInviteId;
            try
            {
                var guestInvite = await _guestInviteController.GetGuestInviteByIdAsync(guestInviteId);
                if (guestInvite == null)
                {
                    return Response.NotFound(ResponseReasons.GuestInviteNotFoundById);
                }

                return guestInvite;
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.GuestInviteNotFoundById);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                //_logger.LogMessage(LogLevel.Error, "GetGuestInviteByIdAsync threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGettingGuestInviteById);
            }
        }
        private async Task<object> GetGuestInvitesByEmail(dynamic input)
        {
            string email = input.guestEmail;
            try
            {
                var guestInvites = await _guestInviteController.GetGuestInvitesByEmailAsync(email);
                if (guestInvites == null)
                {
                    return Response.NotFound(ResponseReasons.GuestInvitesNotFoundByEmail);
                }

                return guestInvites;
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.GuestInvitesNotFoundByEmail);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                //_logger.LogMessage(LogLevel.Error, "GetGuestInvitesByEmailAsync threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGettingGuestInvitesByEmail);
            }
        }
        private async Task<object> GetGuestInvitesByProjectId(dynamic input)
        {
            Guid projectId = input.projectId;
            try
            {
                var guestInvites = await _guestInviteController.GetGuestInvitesByProjectId(projectId);
                if (guestInvites == null)
                {
                    return Response.NotFound(ResponseReasons.GuestInvitesNotFoundByProjectId);
                }

                return guestInvites;
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.GuestInvitesNotFoundByProjectId);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                //_logger.LogMessage(LogLevel.Error, "GetGuestInvitesByProjectIdAsync threw an unhandled exception", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGettingGuestInvitesByProjectId);
            }
        }
        private async Task<object> UpdateGuestInvite(dynamic input)
        {
            GuestInvite updatedGuestInvite;
            try
            {
                updatedGuestInvite = this.Bind<GuestInvite>();
            }
            catch (Exception ex)
            {
                //_logger.LogMessage(LogLevel.Warning, "Binding failed during UpdateGuestInvite", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                return await _guestInviteController.UpdateGuestInviteAsync(updatedGuestInvite);
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.)
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        #endregion


    }
}
