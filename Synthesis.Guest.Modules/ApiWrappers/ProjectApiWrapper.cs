using Synthesis.GuestService.ApiWrappers.Interfaces;
using Synthesis.GuestService.ApiWrappers.Responses;
using Synthesis.Http.Microservice;
using System;
using System.Threading.Tasks;
using Synthesis.GuestService.Models;

namespace Synthesis.GuestService.ApiWrappers
{
    public class ProjectApiWrapper : BaseApiWrapper, IProjectApiWrapper
    {
        public ProjectApiWrapper(IMicroserviceHttpClientResolver httpClient, string serviceUrl) : base(httpClient, serviceUrl)
        {
        }

        public async Task<MicroserviceResponse<Project>> GetProjectByAccessCodeAsync(string projectAccessCode)
        {
            return await HttpClient.GetAsync<Project>($"{ServiceUrl}/v1/projects/{projectAccessCode}");
        }

        public async Task<MicroserviceResponse<Project>> GetProjectByIdAsync(Guid projectId)
        {
            return await HttpClient.GetAsync<Project>($"{ServiceUrl}/v1/projects/{projectId}");
        }
    }
}