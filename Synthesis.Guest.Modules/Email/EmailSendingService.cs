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

        public async Task<MicroserviceResponse> SendGuestInviteEmailAsync(string projectName, string projectCode, string guestEmail, string fromFirstName, string fromLastName)
        {
            var request = InviteGuestEmail.BuildRequest(
                projectName,
                projectCode,
                guestEmail,
                fromFirstName,
                fromLastName,
                _appSettingsReader.GetValue<string>("WebClient.Url"));

            return await _emailApi.SendEmailAsync(request);
        }
    }
}
