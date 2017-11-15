using Synthesis.GuestService.ApiWrappers.Interfaces;
using Synthesis.GuestService.ApiWrappers.Responses;
using Synthesis.Http.Microservice;
using System;
using System.Threading.Tasks;

namespace Synthesis.GuestService.ApiWrappers
{
    public class ProjectApiWrapper : BaseApiWrapper, IProjectApiWrapper
    {
        public ProjectApiWrapper(IMicroserviceHttpClient httpClient, string serviceUrl) : base(httpClient, serviceUrl)
        {
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