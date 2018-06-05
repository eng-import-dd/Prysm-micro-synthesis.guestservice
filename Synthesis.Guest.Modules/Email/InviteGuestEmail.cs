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
        public static SendEmailRequest BuildRequest(string projectName, string projectCode, string guestEmail, string fromFirstName,  string webClientUrl)
        {
            try
            {
                return BuildRequestObject(projectName, projectCode, guestEmail, fromFirstName, webClientUrl);
            }
            catch (Exception ex)
            {
                throw new BuildEmailException($"An error occurred while trying to build the {nameof(SendEmailRequest)}", ex);
            }
        }

        private static SendEmailRequest BuildRequestObject(string projectName, string projectCode, string guestEmail, string firstName, string webClientUrl)
        {
            var subject = "Prysm Guest Invite: " + projectName;
            var link = $"{webClientUrl}/#/guest?accesscode={projectCode}&email={HttpUtility.UrlEncode(guestEmail)}";

            var inviteGuestTemplate = GetContent("Email/Templates/UserInvite.html");
            inviteGuestTemplate = inviteGuestTemplate.Replace("{Link}", link);
            inviteGuestTemplate = inviteGuestTemplate.Replace("{Firstname}", firstName);
            inviteGuestTemplate = inviteGuestTemplate.Replace("{ProjectName}", projectName);
            inviteGuestTemplate = inviteGuestTemplate.Replace("{ProjectCode}", projectCode.Insert(7, " ").Insert(3, " "));

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
