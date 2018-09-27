using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Nancy;
using Nancy.ModelBinding;
using Newtonsoft.Json;
using Synthesis.Guest.ProjectContext.Models;
using Synthesis.GuestService.Constants;
using Synthesis.GuestService.Controllers;
using Synthesis.GuestService.InternalApi.Constants;
using Synthesis.GuestService.InternalApi.Models;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Modules;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PolicyEvaluator;

namespace Synthesis.GuestService.Modules
{
    public sealed class ProjectGuestContextModule : SynthesisModule
    {
        private readonly IProjectGuestContextController _projectGuestContextController;

        public ProjectGuestContextModule(
            IMetadataRegistry metadataRegistry,
            IPolicyEvaluator policyEvaluator,
            IProjectGuestContextController projectGuestContextController,
            ILoggerFactory loggerFactory)
            : base(GuestServiceBootstrapper.ServiceNameShort, metadataRegistry, policyEvaluator, loggerFactory)
        {
            _projectGuestContextController = projectGuestContextController;

            CreateRoute("SetProjectGuestContext", HttpMethod.Post, Routing.ProjectGuestContextRoute, SetProjectGuestContextAsync)
                .Description("Sets the project guest context and creates guest sessions")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(ProjectGuestContext.Example));

            CreateRoute("PromoteGuestUserToProjectMember", HttpMethod.Post, $"{Routing.ProjectsRoute}/{{projectId:guid}}/promote", PromoteGuestUserToProjectMember)
                .Description("Promotes a guest user to a full project member.")
                .StatusCodes(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.InternalServerError)
                .ResponseFormat(JsonConvert.SerializeObject(ProjectGuestContext.Example));
        }

        private async Task<object> SetProjectGuestContextAsync(dynamic input)
        {
            await RequiresAccess()
                .ExecuteAsync(CancellationToken.None);

            Guid projectId = Request.Query.projectid;
            string accesscode = Request.Query.accesscode;
            try
            {
                return await _projectGuestContextController.SetProjectGuestContextAsync(projectId, accesscode, PrincipalId, TenantId);
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set {nameof(ProjectGuestContext)} due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateGuestSession, ex.Message);
            }
        }

        private async Task<object> PromoteGuestUserToProjectMember(dynamic input)
        {
            await RequiresAccess()
                .ExecuteAsync(CancellationToken.None);

            var projectId = Guid.Empty;
            var userToAdd = Guid.Empty;

            try
            {
                projectId = input.projectId;
                userToAdd = this.Bind<Guid>();
            }
            catch (Exception ex)
            {
                Logger.Error($"Binding failed while attempting to promote userId={userToAdd} to full member of projectId={projectId}.", ex);
                return Response.BadRequestBindingException();
            }

            try
            {
                await _projectGuestContextController.PromoteGuestUserToProjectMember(userToAdd, projectId, PrincipalId, TenantId);

                return new Response
                {
                    StatusCode = HttpStatusCode.OK,
                    ReasonPhrase = "Resource has been updated"
                };
            }
            catch (ValidationFailedException ex)
            {
                return Response.BadRequestValidationFailed(ex.Errors);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to set {nameof(ProjectGuestContext)} due to an error", ex);
                return Response.InternalServerError(ResponseReasons.InternalServerErrorCreateGuestSession, ex.Message);
            }
        }
    }
}