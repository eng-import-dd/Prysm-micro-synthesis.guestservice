using System;
using System.Net.Http;
using System.Threading.Tasks;
using Nancy;
using Synthesis.Authentication;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Controllers;
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
            ITokenValidator tokenValidator,
            IPolicyEvaluator policyEvaluator,
            ILoggerFactory loggerFactory, IProjectLobbyStateController projectLobbyStateController) :
            base(GuestServiceBootstrapper.ServiceName, metadataRegistry, tokenValidator, policyEvaluator, loggerFactory)
        {
            _projectLobbyStateController = projectLobbyStateController;

            //this.RequiresAuthentication();

            CreateRoute("GetProjectLobbyStatus", HttpMethod.Get, $"{Routing.ProjectsRoute}/{{projectId:guid}}/{Routing.ProjectLobbyStatePath}", GetProjectLobbyStateAsync);
        }

        private async Task<object> GetProjectLobbyStateAsync(dynamic input)
        {
            var projectId = input.projectId;

            //await RequiresAccess()
            //    .WithPrincipalIdExpansion(ctx => projectId)
            //    .ExecuteAsync(CancellationToken.None);

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