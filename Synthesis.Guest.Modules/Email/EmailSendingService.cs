using System.Threading.Tasks;
using Synthesis.Configuration;
using Synthesis.EmailService.InternalApi.Api;
using Synthesis.Http.Microservice;

namespace Synthesis.GuestService.Email
{
    public class EmailSendingService : IEmailSendingService
    {
        private readonly IEmailApi _emailApi;
        private readonly IAppSettingsReader _appSettingsReader;

        public EmailSendingService(IEmailApi emailApi, IAppSettingsReader appSettingsReader)
        {
            _emailApi = emailApi;
            _appSettingsReader = appSettingsReader;
        }

        public async Task<MicroserviceResponse> SendGuestInviteEmailAsync(string projectName, string projectCode, string guestEmail, string fullName)
        {
            var request = InviteGuestEmail.BuildRequest(
                projectName,
                projectCode,
                guestEmail,
                fullName,
                _appSettingsReader.GetValue<string>("WebClient.Url"));

            return await _emailApi.SendEmailAsync(request);
        }
    }
}
