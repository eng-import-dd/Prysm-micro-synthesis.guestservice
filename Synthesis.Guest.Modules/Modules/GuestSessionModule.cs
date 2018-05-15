using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Nancy;
using Nancy.ModelBinding;
using Newtonsoft.Json;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.GuestService.InternalApi.Requests;
using Synthesis.GuestService.InternalApi.Responses;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Modules;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PolicyEvaluator;

namespace Synthesis.GuestService.Modules
{
    public sealed class GuestSessionModule : SynthesisModule
    {
        private readonly IGuestSessionController _guestSessionController;

        public GuestSessionModule(
            IMetadataRegistry metadataRegistry,
            IPolicyEvaluator policyEvaluator,
            IGuestSessionController guestSessionController,
            ILoggerFactory loggerFactory)
            : base(GuestServiceBootstrapper.ServiceNameShort, metadataRegistry, policyEvaluator, loggerFactory)
        {
            // Init DI
            _guestSessionController = guestSessionController;

            // Initialize Routes
            CreateRoute("CreateGuestSession", HttpMethod.Post, $"{Routing.GuestSessionsRoute}", CreateGuestSessionAsync)
                .Description("Create a specific GuestSession resource.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new GuestSession()));

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

            CreateRoute("VerifyGuest", HttpMethod.Post, Routing.VerifyGuestRoute, async _ => await VerifyGuestAsync())
                .Description("Verify guest resource.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new GuestVerificationResponse()));

            CreateRoute("EmailHost", HttpMethod.Get, $"{Routing.GuestSessionsRoute}/accesscode/{{accdessCode}}/{Routing.EmailHostPath}", EmailHostAsync)
                .Description("Send email to project host.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new SendHostEmailResponse()));
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

        public async Task<object> EmailHostAsync(dynamic input)
        {
            await RequiresAccess().ExecuteAsync(CancellationToken.None);

            try
            {
                return await _guestSessionController.EmailHostAsync(input.accessCode, PrincipalId);
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
                Logger.Error($"Failed to send email to host for project with access code {input.accessCode} due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetGuestSession);
            }
        }
    }
}