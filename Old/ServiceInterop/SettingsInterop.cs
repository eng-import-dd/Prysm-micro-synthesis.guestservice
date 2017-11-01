using System;
using System.Threading.Tasks;
using Synthesis.GuestService.ApiWrappers;
using Synthesis.Http.Microservice;
using Synthesis.Logging;

namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public class SettingsInterop : BaseInterop, ISettingsInterop
    {
        public SettingsInterop(IServiceLocator serviceLocator, IMicroserviceHttpClient httpClient, ILoggerFactory loggerFactory)
            : base(httpClient, loggerFactory)
        {
            ServiceUrl = serviceLocator.SettingsUrl;
        }

        public async Task<SettingsInteropResponse> GetPrincipalSettingsAsync(Guid projectAccountId)
        {
            try
            {
                //TODO: Verify route
                var result = await HttpClient.GetAsync<PrincipalSettings>($"");

                if (!IsSuccess(result))
                {
                    Logger.Warning($"Call to settings service failed to retrieve settings for tenant {projectAccountId}");
                    return new SettingsInteropResponse
                    {
                        ResponseCode = InteropResponseCode.FailRouteCall
                    };
                }

                if (result.Payload != null)
                {
                    return new SettingsInteropResponse
                    {
                        Settings = result.Payload,
                        ResponseCode = InteropResponseCode.Success
                    };
                }

                Logger.Warning($"No settings found for the given tenant {projectAccountId}");
                return new SettingsInteropResponse
                {
                    ResponseCode = InteropResponseCode.NoRecordsReturned
                };
            }
            catch (Exception ex)
            {
                const string message = "Exception thrown calling the settings service to retrieve a settings by tenantId";
                Logger.Error(message, ex);
                return new SettingsInteropResponse
                {
                    ResponseCode = InteropResponseCode.FailException
                };
            }
        }
    }
}