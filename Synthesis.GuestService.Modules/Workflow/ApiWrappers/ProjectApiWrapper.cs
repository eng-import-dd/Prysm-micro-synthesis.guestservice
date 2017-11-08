using System;
using System.Threading.Tasks;
using Synthesis.Http.Microservice;
using Synthesis.Logging;

namespace Synthesis.GuestService.Workflow.ApiWrappers
{
    public class ProjectApiWrapper : BaseApiWrapper, IProjectApiWrapper
    {
        public ProjectApiWrapper(IServiceLocator serviceLocator, IMicroserviceHttpClient microserviceHttpClient, ILoggerFactory loggerFactory)
            : base(microserviceHttpClient, loggerFactory)
        {
            ServiceUrl = serviceLocator.ProjectUrl;
        }

        public async Task<MicroserviceResponse<ProjectResponse>> GetProjectByAccessCodeAsync(string projectAccessCode)
        {
            return await HttpClient.GetAsync<ProjectResponse>($"{ServiceUrl}/v1/projects/{projectAccessCode}");
        }

        public async Task<MicroserviceResponse<ProjectResponse>> GetProjectByIdAsync(Guid projectId)
        {
            return await HttpClient.GetAsync<ProjectResponse>($"{ServiceUrl}/v1/projects/{projectId}");
        }
    }
}