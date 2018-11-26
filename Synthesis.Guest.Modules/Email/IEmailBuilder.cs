using System.Collections.Generic;
using Synthesis.EmailService.InternalApi.Models;

namespace Synthesis.GuestService.Email
{
    public interface IEmailBuilder
    {
        SendEmailRequest BuildRequest(EmailType emailType, string to, string subject, Dictionary<string, string> replacements);
    }
}