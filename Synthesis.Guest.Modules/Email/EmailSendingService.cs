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

        public async Task<MicroserviceResponse> SendGuestInviteEmailAsync(string projectName, string projectUri, string guestEmail, string fromFirstName)
        {
            var request = InviteGuestEmail.BuildRequest(
                projectName,
                projectUri,
                guestEmail,
                fromFirstName
                );

            return await _emailApi.SendEmailAsync(request);
        }
    }
}
