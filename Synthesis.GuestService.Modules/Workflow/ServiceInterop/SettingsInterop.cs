using System;
using System.Threading.Tasks;
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

        public async Task<PrincipalSettings> GetPrincipalSettingsAsync(Guid projectAccountId)
        {
            //TODO: Need to implement error handling instead of returning null
            try
            {
                //TODO: Need to verify route exists for getting a principal by their username
                var result = await HttpClient.GetAsync<PrincipalSettings>($"{ServiceUrl}/v1/projects/{projectAccountId}");

                return IsSuccess(result) ? result.Payload : null;
            }
            catch (Exception ex)
            {
                const string message = "Exception thrown calling the principal service to retrieve settings for a specific accountId";
                Logger.Error(message, ex);
                return null;
            }
        }
    }
}