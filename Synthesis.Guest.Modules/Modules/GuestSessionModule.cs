using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Nancy;
using Nancy.ErrorHandling;
using Nancy.ModelBinding;
using Newtonsoft.Json;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.InternalApi.Constants;
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

            CreateRoute("UpdateGuestSessionState", HttpMethod.Put, Routing.UpdateGuestSessionStateRoute, UpdateGuestSessionStateAsync)
                .Description("Updates the guest session state")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError)
                .RequestFormat(UpdateGuestSessionStateRequest.Example)
                .ResponseFormat(UpdateGuestSessionStateResponse.Example);

            CreateRoute("GetGuestSessions", HttpMethod.Get, $"{Routing.ProjectsRoute}/{{projectId:guid}}/{Routing.GuestSessionsPath}", GetGuestSessionsByProjectIdAsync)
                .Description("Gets all valid GuestSessions for a specific project, excluding those with a GuestState of PromotedToProjectMember.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new List<GuestSession> { new GuestSession() }));

            CreateRoute("GetGuestSessionsByProjectForCurrentUser", HttpMethod.Get, $"{Routing.GetGuestSessionsByProjectForCurrentUserRoute}", GetGuestSessionsByProjectIdForCurrentUserAsync)
                .Description("Gets all valid GuestSessions for a specific project and the requesting user, excluding those with a GuestState of PromotedToProjectMember.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new List<GuestSession> { new GuestSession() }));

            CreateRoute("GetGuestSessionsByProjectForUser", HttpMethod.Get, $"{Routing.GetGuestSessionsByProjectForUserRoute}", GetGuestSessionsByProjectIdForUserAsync)
                .Description("Gets all valid GuestSessions for a specific project and user.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new List<GuestSession> { new GuestSession() }));

            CreateRoute("VerifyGuest", HttpMethod.Post, Routing.VerifyGuestRoute, async _ => await VerifyGuestAsync())
                .Description("Verify guest resource.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new GuestVerificationResponse()));

            CreateRoute("EmailHost", HttpMethod.Get, $"{Routing.GuestSessionsRoute}/{Routing.ProjectsPath}/{{accessCode}}/{Routing.EmailHostPath}", EmailHostAsync)
                .Description("Send email to project host.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new SendHostEmailResponse()));
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
                Logger.Error("Binding failed while attempting to create a GuestSession resource", ex);
                return Response.BadRequestBindingException(ResponseReasons.FailedToBindToRequest);
            }

            await RequiresAccess()
                .WithProjectIdExpansion(context => newGuestSession.ProjectId)
                .ExecuteAsync(CancellationToken.None);

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
            GuestSession session;

            try
            {
                session = await _guestSessionController.GetGuestSessionAsync(input.id);
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

            await RequiresAccess()
                .WithProjectIdExpansion(context => session.ProjectId)
                .ExecuteAsync(CancellationToken.None);

            return session;
        }

        private async Task<object> GetGuestSessionsByProjectIdAsync(dynamic input)
        {
            var projectId = input.projectId;

            await RequiresAccess()
                .WithProjectIdExpansion(ctx => projectId)
                .ExecuteAsync(CancellationToken.None);

            try
            {
                return await _guestSessionController.GetMostRecentValidGuestSessionsByProjectIdAsync(projectId);
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
                Logger.Error($"GuestSessions could not be retrieved for projectId {projectId}", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetGuestInvite);
            }
        }

        private async Task<object> GetGuestSessionsByProjectIdForCurrentUserAsync(dynamic input)
        {
            var projectId = input.projectId;

            await RequiresAccess()
                .WithProjectIdExpansion(ctx => projectId)
                .ExecuteAsync(CancellationToken.None);

            try
            {
                return await _guestSessionController.GetValidGuestSessionsByProjectIdForCurrentUserAsync(projectId, PrincipalId);
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
                Logger.Error($"GuestSessions could not be retrieved for projectId {projectId}", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetGuestInvite);
            }
        }

        private async Task<object> GetGuestSessionsByProjectIdForUserAsync(dynamic input)
        {
            var projectId = input.projectId;
            var userId = input.userId;

            await RequiresAccess()
                .WithProjectIdExpansion(ctx => projectId)
                .ExecuteAsync(CancellationToken.None);

            try
            {
                return await _guestSessionController.GetGuestSessionsByProjectIdForUserAsync(projectId, userId);
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
                Logger.Error($"GuestSessions could not be retrieved for projectId {projectId}", ex);
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
                Logger.Error("Binding failed while attempting to update a GuestSession resource.", ex);
                return Response.BadRequestBindingException(ResponseReasons.FailedToBindToRequest);
            }

            await RequiresAccess()
                .WithProjectIdExpansion(ctx => guestSessionModel.ProjectId)
                .ExecuteAsync(CancellationToken.None);

            try
            {
                return await _guestSessionController.UpdateGuestSessionAsync(guestSessionModel, PrincipalId);
            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception encountered while attempting to update a GuestSession resource", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateGuestSession);
            }
        }

        private async Task<object> UpdateGuestSessionStateAsync(dynamic input)
        {
            UpdateGuestSessionStateRequest guestSessionStateRequest;

            try
            {
                guestSessionStateRequest = this.Bind<UpdateGuestSessionStateRequest>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to update a GuestSession state.", ex);
                return Response.BadRequestBindingException(ResponseReasons.FailedToBindToRequest);
            }

            Guid projectId;
            try
            {
                var guestSession = await _guestSessionController.GetGuestSessionAsync(guestSessionStateRequest.GuestSessionId);
                projectId = guestSession.ProjectId;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error fetching projectId for GuestSession {guestSessionStateRequest.GuestSessionId}", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateGuestSessionState);
            }

            await RequiresAccess()
                .WithProjectIdExpansion(ctx => projectId)
                .ExecuteAsync(CancellationToken.None);

            try
            {
                return await _guestSessionController.UpdateGuestSessionStateAsync(guestSessionStateRequest, PrincipalId);
            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception encountered while attempting to update a GuestSession state", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateGuestSessionState);
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
                Logger.Error("Binding failed while attempting to verify a guest.", ex);
                return Response.BadRequestBindingException(ResponseReasons.FailedToBindToRequest);
            }

            await RequiresAccess()
                .WithProjectIdExpansion(ctx => request.ProjectId)
                .ExecuteAsync(CancellationToken.None);

            try
            {
                return await _guestSessionController.VerifyGuestAsync(request, TenantId);
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