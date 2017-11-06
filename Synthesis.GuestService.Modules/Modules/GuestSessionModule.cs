using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nancy;
using Nancy.ModelBinding;
using Nancy.Security;
using Newtonsoft.Json;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Dao.Models;
using Synthesis.GuestService.Requests;
using Synthesis.GuestService.Responses;
using Synthesis.GuestService.Workflow.ApiWrappers;
using Synthesis.GuestService.Workflow.Controllers;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Validation;

namespace Synthesis.GuestService.Modules
{
    public sealed class GuestSessionModule : NancyModule
    {
        private readonly IGuestSessionController _guestSessionController;
        private readonly ILogger _logger;
        private readonly IMetadataRegistry _metadataRegistry;

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

            // Initialize Routes
            Post(BaseRoutes.GuestSession, CreateGuestSessionAsync, null, "CreateGuestSession");
            Post(BaseRoutes.GuestSessionLegacy, CreateGuestSessionAsync, null, "CreateGuestSessionLegacy");

            Post(BaseRoutes.GuestSession + "/createguest", CreateGuestAsync, null, "CreateGuest");
            Post(BaseRoutes.GuestSessionLegacy + "/createguest", CreateGuestAsync, null, "CreateGuestLegacy");

            Get(BaseRoutes.GuestSession + "/{id:guid}", GetGuestSessionAsync, null, "GetGuestSession");
            Get(BaseRoutes.GuestSessionLegacy + "/{id:guid}", GetGuestSessionAsync, null, "GetGuestSessionLegacy");

            Put(BaseRoutes.GuestSession + "/{id:guid}", UpdateGuestSessionAsync, null, "UpdateGuestSession");
            Put(BaseRoutes.GuestSessionLegacy + "/{id:guid}", UpdateGuestSessionAsync, null, "UpdateGuestSessionLegacy");

            Get(BaseRoutes.GuestSession + "/project/{projectId:guid}", GetGuestSessionsByProjectIdAsync, null, "GetGuestSessions");
            Get(BaseRoutes.GuestSessionLegacy + "/project/{projectId:guid}", GetGuestSessionsByProjectIdAsync, null, "GetGuestSessionsLegacy");

            Get(BaseRoutes.GuestSession + "/projectstatus/{projectId:guid}", GetProjectStatusAsync, null, "GetProjectStatus");
            Get(BaseRoutes.GuestSessionLegacy + "/projectstatus/{projectId:guid}", GetProjectStatusAsync, null, "GetProjectStatusLegacy");

            Post(BaseRoutes.GuestSession + "/verificationemail", SendVerificationEmailAsync, null, "SendVerificationEmail");
            Post(BaseRoutes.GuestSessionLegacy + "/verificationemail", SendVerificationEmailAsync, null, "SendVerificationEmailLegacy");

            Post(BaseRoutes.GuestSession + "/verify", async _ => await VerifyGuestAsync(), null, "VerifyGuest");
            Post(BaseRoutes.GuestSessionLegacy + "/verify", async _ => await VerifyGuestAsync(), null, "VerifyGuestLegacy");

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
                Response = JsonConvert.SerializeObject(new GuestSession()),
                Description = "Create a specific GuestSession resource."
            });

            _metadataRegistry.SetRouteMetadata("CreateGuest", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = JsonConvert.SerializeObject(new GuestCreationResponse()),
                Description = "Create a specific GuestSession resource."
            });

            _metadataRegistry.SetRouteMetadata("GetGuestSession", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = JsonConvert.SerializeObject(new GuestSession()),
                Description = "Retrieve a specific GuestSession resource."
            });

            _metadataRegistry.SetRouteMetadata("UpdateGuestSession", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = JsonConvert.SerializeObject(new GuestSession()),
                Description = "Update a specific GuestSession resource."
            });

            _metadataRegistry.SetRouteMetadata("GetGuestSessions", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = JsonConvert.SerializeObject(new List<GuestSession> { new GuestSession() }),
                Description = "Gets All GuestSessions for a specific Project"
            });

            _metadataRegistry.SetRouteMetadata("GetProjectStatus", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = JsonConvert.SerializeObject(new ProjectStatus()),
                Description = "Retrieve the status of a specific Project resource."
            });

            _metadataRegistry.SetRouteMetadata("SendVerificationEmail", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = JsonConvert.SerializeObject(new GuestVerificationEmailResponse()),
                Description = "Sends a verification email to a specific Guest User resource."
            });

            _metadataRegistry.SetRouteMetadata("VerifyGuest", new SynthesisRouteMetadata
            {
                ValidStatusCodes = new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.InternalServerError },
                Response = JsonConvert.SerializeObject(new GuestVerificationResponse()),
                Description = "Verify guest resource."
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
                _logger.Error("Binding failed while attempting to create a GuestSession resource", ex);
                return Response.BadRequestBindingException(ResponseReasons.FailedToBindToRequest);
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

        public async Task<object> CreateGuestAsync(dynamic input)
        {
            GuestCreationRequest request;

            try
            {
                request = this.Bind<GuestCreationRequest>();
            }
            catch (Exception ex)
            {
                _logger.Error("Binding failed while attempting to create a guest.", ex);
                return Response.BadRequestBindingException(ResponseReasons.FailedToBindToRequest);
            }

            try
            {
                return await _guestSessionController.CreateGuestAsync(request);
            }
            catch (Exception ex)
            {
                _logger.Error("Unhandled exception encountered while attempting to create a guest", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateGuestSession);
            }
        }

        private async Task<object> GetGuestSessionAsync(dynamic input)
        {
            try
            {
                return await _guestSessionController.GetGuestSessionAsync(input.id);
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
                _logger.Error($"Failed to get guestSession with id {input.id} due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetGuestSession);
            }
        }

        private async Task<object> GetGuestSessionsByProjectIdAsync(dynamic input)
        {
            try
            {
                return await _guestSessionController.GetGuestSessionsByProjectIdAsync(input.projectId);
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
                _logger.Error($"GuestSessions could not be retrieved for projectId {input.projectId}", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetGuestInvite);
            }
        }

        private async Task<object> UpdateGuestSessionAsync(dynamic input)
        {
            GuestSession guestSessionModel;

            try
            {
                guestSessionModel = this.Bind<GuestSession>();
            }
            catch (Exception ex)
            {
                _logger.Error("Binding failed while attempting to update a GuestSession resource.", ex);
                return Response.BadRequestBindingException(ResponseReasons.FailedToBindToRequest);
            }

            try
            {
                return await _guestSessionController.UpdateGuestSessionAsync(guestSessionModel);
            }
            catch (Exception ex)
            {
                _logger.Error("Unhandled exception encountered while attempting to update a GuestSession resource", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateGuestSession);
            }
        }

        public async Task<object> GetProjectStatusAsync(dynamic input)
        {
            try
            {
                return await _guestSessionController.GetProjectStatusAsync(input.projectId);
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
                _logger.Error($"Failed to get guestSessions for project with projectId {input.projectId} due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetGuestSession);
            }
        }

        public async Task<object> SendVerificationEmailAsync(dynamic input)
        {
            GuestVerificationEmailRequest guestVerificationEmail;

            try
            {
                guestVerificationEmail = this.Bind<GuestVerificationEmailRequest>();
            }
            catch (Exception ex)
            {
                _logger.Error("Binding failed while attempting to send a verification email.", ex);
                return Response.BadRequestBindingException(ResponseReasons.FailedToBindToRequest);
            }

            try
            {
                return await _guestSessionController.SendVerificationEmailAsync(guestVerificationEmail);
            }
            catch (Exception ex)
            {
                _logger.Error("Unhandled exception encountered while attempting to send a guest verificaiton email", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateGuestSession);
            }
        }

        public async Task<object> VerifyGuestAsync()
        {
            GuestVerificationRequest request;

            try
            {
                request = this.Bind<GuestVerificationRequest>();
            }
            catch (Exception ex)
            {
                _logger.Error("Binding failed while attempting to verify a guest.", ex);
                return Response.BadRequestBindingException(ResponseReasons.FailedToBindToRequest);
            }

            try
            {
                return await _guestSessionController.VerifyGuestAsync(request.Username, request.ProjectAccessCode);
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
                _logger.Error($"Failed to verify guest with username {request.Username} due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetGuestSession);
            }
        }
    }
}