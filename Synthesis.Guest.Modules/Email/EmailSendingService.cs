using System.Collections.Generic;
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
        private readonly IEmailBuilder _emailBuilder;

        public EmailSendingService(IEmailApi emailApi, IEmailBuilder emailBuilder, IAppSettingsReader appSettingsReader)
        {
            _emailApi = emailApi;
            _emailBuilder = emailBuilder;
            _appSettingsReader = appSettingsReader;
        }

        public async Task<MicroserviceResponse> SendGuestInviteEmailAsync(string projectName, string projectUri, string guestEmail, string invitorFirstName)
        {
            var request = _emailBuilder.BuildRequest(EmailType.InviteGuest, guestEmail, $"Prysm Guest Invite: {projectName}",
                new Dictionary<string, string>
                {
                    { "Project", projectName },
                    { "WebClientProjectLink", projectUri },
                    { "InvitorFullName", invitorFirstName }
                });

            return await _emailApi.SendEmailAsync(request);
        }

        public async Task<MicroserviceResponse> SendNotifyHostEmailAsync(string hostEmail, string projectName, string guestFullName, string guestEmail, string guestFirstName)
        {
            var request = _emailBuilder.BuildRequest(EmailType.NotifyHost, hostEmail, "You have a guest waiting for you in the lobby",
                new Dictionary<string, string>
                {
                    { "GuestFullName", guestFullName },
                    { "GuestFirstName", guestFirstName },
                    { "GuestEmail", guestEmail },
                    { "Project", projectName },
                    { "HostEmail", hostEmail },
                    { "WebClientLink", _appSettingsReader.GetValue<string>("WebClient.Url") }
                });

            return await _emailApi.SendEmailAsync(request);
        }
    }
}
