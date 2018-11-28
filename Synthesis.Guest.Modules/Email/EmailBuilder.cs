using System;
using System.Collections.Generic;
using System.IO;
using Synthesis.EmailService.InternalApi.Models;
using Synthesis.GuestService.Exceptions;

namespace Synthesis.GuestService.Email
{
    public class EmailBuilder : IEmailBuilder
    {
        public SendEmailRequest BuildRequest(EmailType emailType, string recipient, string subject, Dictionary<string, string> replacements)
        {
            string templatePath;
            switch (emailType)
            {
                case EmailType.InviteGuest:
                    templatePath = "Email/Templates/GuestInvite.html";
                    break;
                case EmailType.NotifyHost:
                    templatePath = "Email/Templates/EmailHost.html";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(emailType), emailType, null);
            }

            try
            {
                var replacedContent = GetContent(templatePath);
                foreach (var keyValuePair in replacements)
                {
                    replacedContent = replacedContent.Replace("{" + keyValuePair.Key + "}", keyValuePair.Value);
                }

                return new SendEmailRequest
                {
                    To = new List<string> { recipient },
                    Subject = subject,
                    Content = replacedContent
                };
            }
            catch (Exception ex)
            {
                throw new BuildEmailException($"An error occurred while trying to build email {emailType}", ex);
            }
        }

        private static string GetContent(string relativePath)
        {
            var absolutePath = Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, relativePath);
            using (var streamReader = new StreamReader(absolutePath))
            {
                var content = streamReader.ReadToEnd();
                return content;
            }
        }
    }
}