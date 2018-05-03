using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using Synthesis.EmailService.InternalApi.Models;
using System.Web;
using Synthesis.GuestService.Exceptions;

namespace Synthesis.GuestService.Utilities
{
    public class VerifyGuestEmailBuilder
    {
        public static SendEmailRequest BuildRequest(string firstName, string email, string accessCode, string emailVerificationId)
        {
            try
            {
                const string subject = "Almost there! Please verify your Prysm account.";

                var link = $"{ConfigurationManager.AppSettings.Get("BaseWebClientUrl")}/#/guest?" +
                    $"{(string.IsNullOrWhiteSpace(email) ? string.Empty : "accesscode=" + accessCode + "&")}" +
                    $"email={HttpUtility.UrlEncode(email)}&token={emailVerificationId}";

                var createGuestInviteTemplate = GetContent("Utilities/EmailTemplates/VerifyNewAccount.html");
                createGuestInviteTemplate = createGuestInviteTemplate.Replace("{Link}", link);
                createGuestInviteTemplate = createGuestInviteTemplate.Replace("{FirstName}", firstName);

                return new SendEmailRequest
                {
                    To = new List<string> { email },
                    Subject = subject,
                    Content = createGuestInviteTemplate
                };
            }
            catch (Exception ex)
            {
                throw new BuildEmailException($"An error occurred while trying to build the {nameof(SendEmailRequest)}", ex);
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
