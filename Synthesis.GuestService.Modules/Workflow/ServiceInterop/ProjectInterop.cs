using System;
using System.Threading.Tasks;
using Synthesis.Http.Microservice;
using Synthesis.Logging;

namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public class ProjectInterop : BaseInterop, IProjectInterop
    {
        public ProjectInterop(IServiceLocator serviceLocator, IMicroserviceHttpClient httpClient, ILoggerFactory loggerFactory)
            : base(httpClient, loggerFactory)
        {
            ServiceUrl = serviceLocator.ProjectUrl;
        }

        public async Task<Project> GetProjectByAccessCodeAsync(string projectAccessCode)
        {
            //TODO: Need to implement error handling instead of returning null
            try
            {
                var result = await HttpClient.GetAsync<Project>($"{ServiceUrl}/v1/projects/{projectAccessCode}");

                return IsSuccess(result) ? result.Payload : null;
            }
            catch (Exception ex)
            {
                const string message = "Exception thrown calling the project service to retrieve a project by projectAccessCode";
                Logger.Error(message, ex);
                return null;
            }
        }

        public async Task<Project> GetProjectByIdAsync(Guid projectId)
        {
            //TODO: Need to implement error handling instead of returning null
            try
            {
                var result = await HttpClient.GetAsync<Project>($"{ServiceUrl}/v1/projects/{projectId}");

                return IsSuccess(result) ? result.Payload : null;
            }
            catch (Exception ex)
            {
                const string message = "Exception thrown calling the project service to retrieve a project by projectAccessCode";
                Logger.Error(message, ex);
                return null;
            }
        }
    }
}