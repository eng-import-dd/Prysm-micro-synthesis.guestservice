using System;
using System.Linq;
using System.Threading.Tasks;
using Synthesis.GuestService.ApiWrappers;
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

        public async Task<ProjectInteropResponse> GetProjectByAccessCodeAsync(string projectAccessCode)
        {
            try
            {
                // TODO: Verify route
                var result = await HttpClient.GetManyAsync<Project>($"{ServiceUrl}/v1/projects/{projectAccessCode}");

                if (!IsSuccess(result))
                {
                    Logger.Warning($"Call to project service failed to retrieve project with access code {projectAccessCode}");
                    return new ProjectInteropResponse
                    {
                        ResponseCode = InteropResponseCode.FailRouteCall
                    };
                }

                if (result.Payload.Any())
                {
                    return new ProjectInteropResponse
                    {
                        ProjectList = result.Payload.ToList(),
                        ResponseCode = InteropResponseCode.Success
                    };
                }

                Logger.Warning($"No project records found for access code {projectAccessCode}");
                return new ProjectInteropResponse
                {
                    ResponseCode = InteropResponseCode.NoRecordsReturned
                };
            }
            catch (Exception ex)
            {
                const string message = "Exception thrown calling the project service to retrieve projects by access code";
                Logger.Error(message, ex);
                return new ProjectInteropResponse
                {
                    ResponseCode = InteropResponseCode.FailException
                };
            }
        }

        public async Task<ProjectInteropResponse> GetProjectByIdAsync(Guid projectId)
        {
            try
            {
                // TODO: Verify route
                var result = await HttpClient.GetManyAsync<Project>($"{ServiceUrl}/v1/projects/{projectId}");

                if (!IsSuccess(result))
                {
                    Logger.Warning($"Call to project service failed to retrieve project with projectId {projectId}");
                    return new ProjectInteropResponse
                    {
                        ResponseCode = InteropResponseCode.FailRouteCall
                    };
                }

                if (result.Payload.Any())
                {
                    return new ProjectInteropResponse
                    {
                        ProjectList = result.Payload.ToList(),
                        ResponseCode = InteropResponseCode.Success
                    };
                }

                Logger.Warning($"No project records found for projectId {projectId}");
                return new ProjectInteropResponse
                {
                    ResponseCode = InteropResponseCode.NoRecordsReturned
                };
            }
            catch (Exception ex)
            {
                const string message = "Exception thrown calling the project service to retrieve projects by projectId";
                Logger.Error(message, ex);
                return new ProjectInteropResponse
                {
                    ResponseCode = InteropResponseCode.FailException
                };
            }
        }
    }
}