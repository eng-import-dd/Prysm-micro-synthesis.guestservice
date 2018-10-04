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
using Synthesis.GuestService.Exceptions;
using Synthesis.GuestService.InternalApi.Constants;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Modules;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PolicyEvaluator;

namespace Synthesis.GuestService.Modules
{
    public sealed class GuestInviteModule : SynthesisModule
    {
        private readonly IGuestInviteController _guestInviteController;

        public GuestInviteModule(
            IMetadataRegistry metadataRegistry,
            IPolicyEvaluator policyEvaluator,
            IGuestInviteController guestInviteController,
            ILoggerFactory loggerFactory)
            : base(GuestServiceBootstrapper.ServiceNameShort, metadataRegistry, policyEvaluator, loggerFactory)
        {
            // Init DI
            _guestInviteController = guestInviteController;

            // initialize routes
            CreateRoute("CreateGuestInvite", HttpMethod.Post, Routing.GuestInvitesRoute, CreateGuestInviteAsync)
                .Description("Create a specific GuestInvite resource.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new GuestInvite()));

            CreateRoute("GetGuestInvite", HttpMethod.Get, $"{Routing.GuestInvitesRoute}/{{id:guid}}", GetGuestInviteAsync)
                .Description("Retrieves a specific GuestInvite resource.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new GuestInvite()));

            CreateRoute("GetGuestInvites", HttpMethod.Get, $"{Routing.ProjectsRoute}/{{projectId:guid}}/{Routing.GuestInvitesPath}", GetValidGuestInvitesByProjectIdAsync)
                .Description("Gets All GuestInvites for a specific Project.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new List<GuestInvite> { new GuestInvite() }));

            CreateRoute("GetGuestInvitesForUserAsync", HttpMethod.Post, $"{Routing.UsersRoute}/{Routing.GuestInvitesPath}", GetGuestInvitesForUserAsync)
                .Description("Gets All GuestInvites for a specific User.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new List<GuestInvite> { new GuestInvite() }));

            CreateRoute("UpdateGuestInvite", HttpMethod.Put, $"{Routing.GuestInvitesRoute}/{{id:guid}}", UpdateGuestInviteAsync)
                .Description("Update a specific GuestInvite resource.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new GuestInvite()));

            CreateRoute("IsGuestRegistrationRequired", HttpMethod.Get, $"{Routing.GuestInvitesRoute}/{Routing.IsGuestRegistrationRequiredPath}", IsGuestRegistrationRequired)
                .Description("Check if the guest user require registration.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(true));
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
                Logger.Error("Binding failed while attempting to create a GuestInvite resource", ex);
                return Response.BadRequestBindingException(ResponseReasons.FailedToBindToRequest);
            }

            await RequiresAccess()
                .WithProjectIdExpansion(ctx => newGuestInvite.ProjectId)
                .ExecuteAsync(CancellationToken.None);

            try
            {
                return await _guestInviteController.CreateGuestInviteAsync(newGuestInvite, TenantId);
            }
            catch (ValidationFailedException ex)
            {
                Logger.Error("Validation failed while attempting to create a GuestInvite resource.");
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (GetProjectException ex)
            {
                Logger.Error("Failed to get the project the guest is being invited to due to an error", ex);
                return Response.InternalServerError("Failed to get the project the guest is being invited to.");
            }
            catch (GetUserException ex)
            {
                Logger.Error("Failed to get the user who invited the guest due to an error", ex);
                return Response.InternalServerError("Failed to get the user who invited the guest.");
            }
            catch (ResetAccessCodeException ex)
            {
                Logger.Error("Failed to reset the guest access code due to an error.", ex);
                return Response.InternalServerError("Failed to reset the guest access code.");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to create guestInvite resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateGuestInvite);
            }
        }

        private async Task<object> GetGuestInviteAsync(dynamic input)
        {
            GuestInvite result;
            try
            {
                result = await _guestInviteController.GetGuestInviteAsync(input.id);
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
                Logger.Error($"Failed to get guestInvite with id {input.id} due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetGuestInvite);
            }

            await RequiresAccess()
                .WithProjectIdExpansion(ctx => result.ProjectId)
                .ExecuteAsync(CancellationToken.None);

            return result;
        }

        private async Task<object> GetValidGuestInvitesByProjectIdAsync(dynamic input)
        {
            var projectId = input.projectId;

            await RequiresAccess()
                .WithProjectIdExpansion(ctx => projectId)
                .ExecuteAsync(CancellationToken.None);

            try
            {
                return await _guestInviteController.GetValidGuestInvitesByProjectIdAsync(projectId);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error($"GuestInvites could not be retrieved for projectId {projectId}", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetGuestInvite);
            }
        }

        private async Task<object> GetGuestInvitesForUserAsync(dynamic input)
        {
            GetGuestInvitesRequest getUserInvitesRequest;
            try
            {
                getUserInvitesRequest = this.Bind<GetGuestInvitesRequest>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding to the SendEmailRequest model failed.", ex);
                return Response.BadRequestBindingException(ResponseReasons.FailedToBindToRequest);
            }

            await RequiresAccess()
                .ExecuteAsync(CancellationToken.None);

            try
            {
                return await _guestInviteController.GetGuestInvitesForUserAsync(getUserInvitesRequest);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error("GuestInvites could not be retrieved for user", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetGuestInvite);
            }
        }

        private async Task<object> UpdateGuestInviteAsync(dynamic input)
        {
            GuestInvite guestInviteModel;

            try
            {
                guestInviteModel = this.Bind<GuestInvite>();
            }
            catch (Exception ex)
            {
                Logger.Error("Binding failed while attempting to update a GuestInvite resource.", ex);
                return Response.BadRequestBindingException(ResponseReasons.FailedToBindToRequest);
            }

            await RequiresAccess()
                .WithProjectIdExpansion(ctx => guestInviteModel.ProjectId)
                .ExecuteAsync(CancellationToken.None);

            try
            {
                return await _guestInviteController.UpdateGuestInviteAsync(guestInviteModel);
            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception encountered while attempting to update a GuestInvite resource", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateGuestInvite);
            }
        }

        private async Task<object> IsGuestRegistrationRequired(dynamic input)
        {
            try
            {
                var email = Request.Query.email;
                var accessCode = Request.Query.accesscode;
                return await _guestInviteController.IsGuestRegistrationRequired(email, accessCode);
            }
            catch (NotFoundException)
            {
                return Response.NotFound(ResponseReasons.NotFoundGuestInvite);
            }
            catch (Exception ex)
            {
                Logger.Error("Unhandled exception encountered while attempting to update a GuestInvite resource", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorUpdateGuestInvite);
            }
        }
    }
}