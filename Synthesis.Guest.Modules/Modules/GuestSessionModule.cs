using Nancy;
using Nancy.ModelBinding;
using Nancy.Security;
using Newtonsoft.Json;
using Synthesis.Authentication;
using Synthesis.GuestService.ApiWrappers.Requests;
using Synthesis.GuestService.ApiWrappers.Responses;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.Models;
using Synthesis.GuestService.Requests;
using Synthesis.GuestService.Responses;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Modules;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PolicyEvaluator;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Synthesis.GuestService.Modules
{
    public sealed class GuestSessionModule : SynthesisModule
    {
        private readonly IGuestSessionController _guestSessionController;

        public GuestSessionModule(
            IMetadataRegistry metadataRegistry,
            ITokenValidator tokenValidator,
            IPolicyEvaluator policyEvaluator,
            IGuestSessionController guestSessionController,
            ILoggerFactory loggerFactory)
            : base(GuestServiceBootstrapper.ServiceName, metadataRegistry, tokenValidator, policyEvaluator, loggerFactory)
        {
            // Init DI
            _guestSessionController = guestSessionController;

            this.RequiresAuthentication();

            // Initialize Routes
            CreateRoute("CreateGuestSession", HttpMethod.Post, $"{Routing.GuestSessionsRoute}", CreateGuestSessionAsync)
                .Description("Create a specific GuestSession resource.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new GuestSession()));

            CreateRoute("CreateGuest", HttpMethod.Post, $"{Routing.GuestSessionsRoute}/createguest", CreateGuestAsync)
                .Description("Creates a Guest user from a guest session.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new GuestCreationResponse()));

            CreateRoute("GetGuestSession", HttpMethod.Get, $"{Routing.GuestSessionsRoute}/{{id:guid}}", GetGuestSessionAsync)
                .Description("Retrieve a specific GuestSession resource.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized,HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new GuestSession()));

            CreateRoute("UpdateGuestSession", HttpMethod.Put, $"{Routing.GuestSessionsRoute}/{{id:guid}}", UpdateGuestSessionAsync)
                .Description("Update a specific GuestSession resource.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new GuestSession()));

            CreateRoute("GetGuestSessions", HttpMethod.Get, $"{Routing.ProjectsRoute}/{{projectId:guid}}/{Routing.GuestSessionsPath}", GetGuestSessionsByProjectIdAsync)
                .Description("Gets All GuestSessions for a specific Project")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new List<GuestSession> { new GuestSession() }));

            CreateRoute("GetProjectStatus", HttpMethod.Get, $"{Routing.ProjectsRoute}/{{projectId:guid}}/{Routing.ProjectStatusPath}", GetProjectStatusAsync)
                .Description("Retrieve the status of a specific Project resource.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new ProjectStatus()));

            CreateRoute("SendVerificationEmail", HttpMethod.Post, Routing.VerificationEmailRoute, async _ => await SendVerificationEmailAsync())
                .Description("Sends a verification email to a specific Guest User resource.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new GuestVerificationEmailResponse()));

            CreateRoute("VerifyGuest", HttpMethod.Post, Routing.VerifyGuestRoute, async _ => await VerifyGuestAsync())
                .Description("Verify guest resource.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new GuestVerificationResponse()));

            OnError += (ctx, ex) =>
            {
                Logger.Error($"Unhandled exception while executing route {ctx.Request.Path}", ex);
                return Response.InternalServerError(ex.Message);
            };
        }

        private async Task<object> CreateGuestSessionAsync(dynamic input)
        {
            await RequiresAccess().ExecuteAsync(CancellationToken.None);

            GuestSession newGuestSession;
            try
            {
                newGuestSession = this.Bind<GuestSession>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to create a GuestSession resource", ex);
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
                Logger.Error("Failed to create guestSession resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateGuestSession);
            }
        }

        public async Task<object> CreateGuestAsync(dynamic input)
        {
            // This route is being deleted. No reason to add auth.
            // TODO - remove once CU-XXX is complete
            GuestCreationRequest request;

            try
            {
                request = this.Bind<GuestCreationRequest>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to create a guest.", ex);
                return Response.BadRequestBindingException(ResponseReasons.FailedToBindToRequest);
            }

            try
            {
                return await _guestSessionController.CreateGuestAsync(request);
            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception encountered while attempting to create a guest", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateGuestSession);
            }
        }

        private async Task<object> GetGuestSessionAsync(dynamic input)
        {
            await RequiresAccess().ExecuteAsync(CancellationToken.None);

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
                Logger.Error($"Failed to get guestSession with id {input.id} due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetGuestSession);
            }
        }

        private async Task<object> GetGuestSessionsByProjectIdAsync(dynamic input)
        {
            var projectId = input.projectId;

            await RequiresAccess()
                .WithProjectIdExpansion(ctx => projectId)
                .ExecuteAsync(CancellationToken.None);

            try
            {
                return await _guestSessionController.GetGuestSessionsByProjectIdAsync(projectId);
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
                Logger.Error($"GuestSessions could not be retrieved for projectId {projectId}", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetGuestInvite);
            }
        }

        private async Task<object> UpdateGuestSessionAsync(dynamic input)
        {
            await RequiresAccess().ExecuteAsync(CancellationToken.None);

            GuestSession guestSessionModel;

            try
            {
                guestSessionModel = this.Bind<GuestSession>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to update a GuestSession resource.", ex);
                return Response.BadRequestBindingException(ResponseReasons.FailedToBindToRequest);
            }

            try
            {
                return await _guestSessionController.UpdateGuestSessionAsync(guestSessionModel);
            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception encountered while attempting to update a GuestSession resource", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateGuestSession);
            }
        }

        public async Task<object> GetProjectStatusAsync(dynamic input)
        {
            var projectId = input.projectId;

            await RequiresAccess()
                .WithProjectIdExpansion(ctx => projectId)
                .ExecuteAsync(CancellationToken.None);

            try
            {
                return await _guestSessionController.GetProjectStatusAsync(projectId);
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
                Logger.Error($"Failed to get guestSessions for project with projectId {projectId} due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetGuestSession);
            }
        }

        public async Task<object> SendVerificationEmailAsync()
        {
            await RequiresAccess().ExecuteAsync(CancellationToken.None);

            GuestVerificationEmailRequest guestVerificationEmailRequest;

            try
            {
                guestVerificationEmailRequest = this.Bind<GuestVerificationEmailRequest>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to send a verification email.", ex);
                return Response.BadRequestBindingException(ResponseReasons.FailedToBindToRequest);
            }

            try
            {
                return await _guestSessionController.SendVerificationEmailAsync(guestVerificationEmailRequest);
            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception encountered while attempting to send a guest verificaiton email", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateGuestSession);
            }
        }

        public async Task<object> VerifyGuestAsync()
        {
            await RequiresAccess().ExecuteAsync(CancellationToken.None);

            GuestVerificationRequest request;

            try
            {
                request = this.Bind<GuestVerificationRequest>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to verify a guest.", ex);
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
                Logger.Error($"Failed to verify guest with username {request.Username} due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetGuestSession);
            }
        }
    }
}