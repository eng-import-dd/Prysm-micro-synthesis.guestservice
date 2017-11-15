using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Nancy;
using Nancy.ModelBinding;
using Nancy.Security;
using Newtonsoft.Json;
using Synthesis.Authentication;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.Models;
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
            ITokenValidator tokenValidator,
            IPolicyEvaluator policyEvaluator,
            IGuestInviteController guestInviteController,
            ILoggerFactory loggerFactory)
            : base(GuestServiceBootstrapper.ServiceName, metadataRegistry, tokenValidator, policyEvaluator, loggerFactory)
        {
            // Init DI
            _guestInviteController = guestInviteController;

            this.RequiresAuthentication();

            // initialize routes
            CreateRoute("CreateGuestInvite", HttpMethod.Post, Routing.GuestInvitesRoute, CreateGuestInviteAsync)
                .Description("Create a specific GuestInvite resource.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new GuestInvite()));

            CreateRoute("GetGuestInvite", HttpMethod.Get, $"{Routing.GuestInvitesRoute}/{{id:guid}}", GetGuestInviteAsync)
                .Description("Retrieves a specific GuestInvite resource.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new GuestInvite()));

            CreateRoute("GetGuestInvites", HttpMethod.Get, $"{Routing.ProjectsRoute}/{{projectId:guid}}/{Routing.GuestInvitesPath}", GetGuestInvitesByProjectIdAsync)
                .Description("Gets All GuestInvites for a specific Project.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new List<GuestInvite> { new GuestInvite() }));

            CreateRoute("UpdateGuestInvite", HttpMethod.Put, $"{Routing.GuestInvitesRoute}/{{id:guid}}", UpdateGuestInviteAsync)
                .Description("Update a specific GuestInvite resource.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new GuestInvite()));
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
                Logger.Error("Failed to create guestInvite resource due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateGuestInvite);
            }
        }

        private async Task<object> GetGuestInviteAsync(dynamic input)
        {
            try
            {
                return await _guestInviteController.GetGuestInviteAsync(input.id);
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
        }

        private async Task<object> GetGuestInvitesByProjectIdAsync(dynamic input)
        {
            try
            {
                return await _guestInviteController.GetGuestInvitesByProjectIdAsync(input.projectId);
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
                Logger.Error($"GuestInvites could not be retrieved for projectId {input.projectId}", ex);
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
    }
}