using System;
using System.Collections.Generic;
using System.IO;
using Nancy.Helpers;
using Synthesis.EmailService.InternalApi.Models;
using Synthesis.GuestService.Exceptions;

namespace Synthesis.GuestService.Email
{
    public class InviteGuestEmail
    {
        public static SendEmailRequest BuildRequest(string projectName, string projectUri, string guestEmail, string fromFirstName)
        {
            try
            {
                return BuildRequestObject(projectName, projectUri, guestEmail, fromFirstName);
            }
            catch (Exception ex)
            {
                throw new BuildEmailException($"An error occurred while trying to build the {nameof(SendEmailRequest)}", ex);
            }
        }

        private static SendEmailRequest BuildRequestObject(string projectName, string projectUri, string guestEmail, string fromFirstName)
        {
            var subject = "Prysm Guest Invite: " + projectName;

            var inviteGuestTemplate = GetContent("Email/Templates/GuestInvite.html");
            inviteGuestTemplate = inviteGuestTemplate.Replace("{Link}", $"{projectUri}&email={HttpUtility.UrlEncode(guestEmail)}");
            inviteGuestTemplate = inviteGuestTemplate.Replace("{Name}", fromFirstName);
            inviteGuestTemplate = inviteGuestTemplate.Replace("{ProjectName}", projectName);

            return new SendEmailRequest
            {
                To = new List<string> { guestEmail },
                Subject = subject,
                Content = inviteGuestTemplate
            };
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
