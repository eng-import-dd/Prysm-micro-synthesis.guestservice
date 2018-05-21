﻿using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Nancy;
using Newtonsoft.Json;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Controllers;
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
    public class ProjectLobbyStateModule : SynthesisModule
    {
        private readonly IProjectLobbyStateController _projectLobbyStateController;

        /// <inheritdoc />
        public ProjectLobbyStateModule(IMetadataRegistry metadataRegistry,
            IPolicyEvaluator policyEvaluator,
            ILoggerFactory loggerFactory, IProjectLobbyStateController projectLobbyStateController) :
            base(GuestServiceBootstrapper.ServiceNameShort, metadataRegistry, policyEvaluator, loggerFactory)
        {
            _projectLobbyStateController = projectLobbyStateController;

            CreateRoute("GetProjectLobbyState", HttpMethod.Get, $"{Routing.ProjectsRoute}/{{projectId:guid}}/{Routing.ProjectLobbyStatePath}", GetProjectLobbyStateAsync)
                .Description("Retrieves lobby state for a project.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(new ProjectLobbyState()));
        }

        private async Task<object> GetProjectLobbyStateAsync(dynamic input)
        {
            var projectId = input.projectId;

            await RequiresAccess()
                .WithPrincipalIdExpansion(ctx => projectId)
                .ExecuteAsync(CancellationToken.None);

            try
            {
                return await _projectLobbyStateController.GetProjectLobbyStateAsync(projectId);
            }
            catch (ValidationFailedException ex)
            {
                Logger.Error($"Validation failed during retrieval of lobby state for project: {projectId}", ex);
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (NotFoundException ex)
            {
                Logger.Error($"Could not find project lobby state for project: {projectId}", ex);
                return Response.NotFound(ResponseReasons.NotFoundProjectLobbyState);
            }
            catch (Exception ex)
            {
                Logger.Error($"An error occurred retrieving lobby state for project: {projectId}", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorGetProjectLobbyState);
            }
        }
    }
}