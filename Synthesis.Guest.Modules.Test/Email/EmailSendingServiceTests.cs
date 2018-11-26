using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Synthesis.Configuration;
using Synthesis.EmailService.InternalApi.Api;
using Synthesis.EmailService.InternalApi.Models;
using Synthesis.GuestService.Email;
using Xunit;

namespace Synthesis.GuestService.Modules.Test.Email
{
    public class EmailSendingServiceTests
    {
        private readonly EmailSendingService _target;
        private readonly Mock<IEmailBuilder> _emailBuilderMock = new Mock<IEmailBuilder>();
        private readonly Mock<IEmailApi> _emailApiMock = new Mock<IEmailApi>();
        private readonly Mock<IAppSettingsReader> _appSettingsReaderMock = new Mock<IAppSettingsReader>();
        private const string DefaultEmail = "aeiou@and.sometimesy";
        private const string DefaultUri = "http://theproject";
        private const string DefaultName = "Jimbob";
        private const string DefaultProject = "Increase revenue";

        public EmailSendingServiceTests()
        {
            _target = new EmailSendingService(_emailApiMock.Object, _emailBuilderMock.Object, _appSettingsReaderMock.Object);
            _emailBuilderMock
                .Setup(x => x.BuildRequest(It.IsAny<EmailType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
                .Returns(new SendEmailRequest());
        }

        [Fact]
        public async Task InviteGuestSendsEmail()
        {
            await _target.SendGuestInviteEmailAsync(DefaultName, DefaultUri, DefaultEmail, DefaultProject);

            _emailApiMock.Verify(x => x.SendEmailAsync(It.IsAny<SendEmailRequest>()));
        }

        [Fact]
        public async Task InviteGuesttBuildsRequestForCorrectEmailTemplate()
        {
            await _target.SendGuestInviteEmailAsync(DefaultName, DefaultUri, DefaultEmail, DefaultProject);

            _emailBuilderMock.Verify(x => x.BuildRequest(It.Is<EmailType>(et => et == EmailType.InviteGuest),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()));
        }

        [Fact]
        public async Task SendNotifyHostSendsEmail()
        {
            await _target.SendNotifyHostEmailAsync(DefaultEmail, DefaultProject, DefaultName, DefaultEmail, DefaultName);

            _emailApiMock.Verify(x => x.SendEmailAsync(It.IsAny<SendEmailRequest>()));
        }

        [Fact]
        public async Task SendNotifyHostBuildsRequestForCorrectEmailTemplate()
        {
            await _target.SendNotifyHostEmailAsync(DefaultEmail, DefaultProject, DefaultName, DefaultEmail, DefaultName);

            _emailBuilderMock.Verify(x => x.BuildRequest(It.Is<EmailType>(et => et == EmailType.NotifyHost),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()));
        }
    }
}
