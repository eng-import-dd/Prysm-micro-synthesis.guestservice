﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net.Mail;
using System.Web;
using Synthesis.Logging;

namespace Synthesis.GuestService.Workflow.Utilities
{
    public class EmailUtility : IEmailUtility
    {
        private readonly ILogger _loggingService;

        private readonly string _emailTemplate;
        private readonly string _guestInviteEmail;
        private readonly string _createGuestInviteEmail;
        private readonly string _emailHostEmail;

        private readonly LinkedResource _facebookIcon;
        private readonly LinkedResource _googlePlusIcon;
        private readonly LinkedResource _linkedInIcon;
        private readonly LinkedResource _prysmLogo;
        private readonly LinkedResource _twitterIcon;
        private readonly LinkedResource _youtubeIcon;

        private readonly List<LinkedResource> _linkedResources = new List<LinkedResource>();

        public EmailUtility(ILogger loggingService)
        {
            _loggingService = loggingService;

            _prysmLogo = new LinkedResource(MapPath("Synthesis/GuestService/Workflow/Utilities/EmailTemplates/Images/Prysm-logo.png"), "image/png");
            _facebookIcon = new LinkedResource(MapPath("Synthesis/GuestService/Workflow/Utilities/EmailTemplates/Images/facebook-icon.png"), "image/png");
            _googlePlusIcon = new LinkedResource(MapPath("Synthesis/GuestService/Workflow/Utilities/EmailTemplates/Images/google-plus-icon.png"), "image/png");
            _linkedInIcon = new LinkedResource(MapPath("Synthesis/GuestService/Workflow/Utilities/EmailTemplates/Images/linkedin-icon.png"), "image/png");
            _twitterIcon = new LinkedResource(MapPath("Synthesis/GuestService/Workflow/Utilities/EmailTemplates/Images/twitter-icon.png"), "image/png");
            _youtubeIcon = new LinkedResource(MapPath("Synthesis/GuestService/Workflow/Utilities/EmailTemplates/Images/youtube-icon.png"), "image/png");

            _linkedResources.Add(_facebookIcon);
            _linkedResources.Add(_googlePlusIcon);
            _linkedResources.Add(_linkedInIcon);
            _linkedResources.Add(_prysmLogo);
            _linkedResources.Add(_twitterIcon);
            _linkedResources.Add(_youtubeIcon);

            using (var streamReader = new StreamReader(MapPath("Synthesis/GuestService/Workflow/Utilities/EmailTemplates/EmailTemplate.html")))
            {
                _emailTemplate = streamReader.ReadToEnd();
            }

            _guestInviteEmail = GetContent("Synthesis/GuestService/Workflow/Utilities/EmailTemplates/GuestInvite.html");
            _createGuestInviteEmail = GetContent("Synthesis/GuestService/Workflow/Utilities/EmailTemplates/VerifyNewAccount.html");
            _emailHostEmail = GetContent("Synthesis/GuestService/Workflow/Utilities/EmailTemplates/EmailHost.html");
        }

        public bool SendGuestInvite(string projectName, string projectCode, string guestEmail, string from)
        {
            try
            {
                var subject = "Prysm Guest Invite: " + projectName;

                var replacedContent = _guestInviteEmail.Replace("{Link}", $"{ConfigurationManager.AppSettings.Get("BaseWebClientUrl")}/#/guest?accesscode={projectCode}&email={HttpUtility.UrlEncode(guestEmail)}");

                replacedContent = replacedContent.Replace("{Name}", from);
                replacedContent = replacedContent.Replace("{ProjectName}", projectName);
                replacedContent = replacedContent.Replace("{ProjectCode}", projectCode.Insert(7, " ").Insert(3, " "));

                SendEmail(guestEmail, "", "", subject, replacedContent, "");
            }
            catch (Exception ex)
            {
                _loggingService.Error(ex.Message, ex);
                return false;
            }

            return true;
        }

        public bool SendVerifyAccountEmail(string firstName, string email, string accessCode, string emailVerificationId)
        {
            try
            {
                const string subject = "Almost there! Please verify your Prysm account.";

                var link = $"{ConfigurationManager.AppSettings.Get("BaseWebClientUrl")}/#/guest?" +
                           $"{(string.IsNullOrWhiteSpace(email) ? string.Empty : "accesscode=" + accessCode + "&")}" +
                           $"email={HttpUtility.UrlEncode(email)}&token={emailVerificationId}";

                var replacedContent = _createGuestInviteEmail.Replace("{Link}", link);
                replacedContent = replacedContent.Replace("{FirstName}", firstName);

                SendEmail(email, "", "", subject, replacedContent, "");
            }
            catch (Exception ex)
            {
                _loggingService.Error(ex.Message, ex);
                return false;
            }

            return true;
        }

        public bool SendHostEmail(string email, string userFullName, string userFirstName, string userEmail, string projectName)
        {
            try
            {
                const string subject = "You have a guest waiting for you in the lobby";

                var replacedContent = _emailHostEmail.Replace("{FullName}", userFullName);
                replacedContent = replacedContent.Replace("{Project}", projectName);
                replacedContent = replacedContent.Replace("{HostEmail}", userEmail);
                replacedContent = replacedContent.Replace("{FirstName}", userFirstName);
                replacedContent = replacedContent.Replace("{WebClientLink}", ConfigurationManager.AppSettings.Get("BaseWebClientUrl"));

                SendEmail(email, "", "", subject, replacedContent, "");
            }
            catch (Exception ex)
            {
                _loggingService.Error(ex.Message, ex);
                return false;
            }

            return true;
        }

        private void SendEmail(string toEmail, string ccEmail, string bccEmail, string subject, string htmlBody,
            string textBody)
        {
            string[] to = { toEmail };
            string[] cc = { ccEmail };
            string[] bcc = { bccEmail };

            SendEmail(to, cc, bcc, subject, htmlBody, textBody, true, new List<Attachment>());
        }

        private void SendEmail(IEnumerable<string> toEmail, IEnumerable<string> ccEmail, IEnumerable<string> bccEmail, string subject,
            string htmlBody, string textBody, bool asHtml, IEnumerable<Attachment> attachments)
        {
            var message = new MailMessage();

            foreach (var email in toEmail)
            {
                message.To.Add(new MailAddress(email));
            }

            foreach (var email in ccEmail)
            {
                if (email != "")
                {
                    message.CC.Add(new MailAddress(email));
                }
            }

            foreach (var email in bccEmail)
            {
                if (email != "")
                {
                    message.Bcc.Add(new MailAddress(email));
                }
            }

            message.Priority = MailPriority.Normal;
            message.IsBodyHtml = asHtml;
            message.Subject = subject;
            var plain = AlternateView.CreateAlternateViewFromString(textBody, new System.Net.Mime.ContentType("text/plain"));
            var html = AlternateView.CreateAlternateViewFromString(htmlBody, new System.Net.Mime.ContentType("text/html"));
            message.AlternateViews.Add(plain);
            message.AlternateViews.Add(html);

            foreach (var attachment in attachments)
            {
                message.Attachments.Add(attachment);

                // Inline and non-inline attachements can't share a stream
                var duplicateStream = new MemoryStream();
                attachment.ContentStream.CopyTo(duplicateStream);
                attachment.ContentStream.Position = 0;
                duplicateStream.Position = 0;

                var resource = new LinkedResource(duplicateStream, "image/png")
                {
                    ContentId = attachment.Name
                };
                html.LinkedResources.Add(resource);
            }

            foreach (var linkedResource in _linkedResources)
            {
                html.LinkedResources.Add(linkedResource);
            }

            var client = new SmtpClient();

            try
            {
                client.Send(message);
            }
            catch (Exception ex)
            {
                _loggingService.Error("First Attempt of sending email failed", ex);
                client.Send(message);
                _loggingService.Info("Second Attempt of sending email succeeded", ex);
            }
        }

        private static string MapPath(string relativePath)
        {
            return Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, relativePath);
        }

        private string GetContent(string path)
        {
            using (var streamReader = new StreamReader(MapPath(path)))
            {
                var content = streamReader.ReadToEnd();
                content = _emailTemplate.Replace("{{CONTENT}}", content);
                content = AddLinkedResources(content);
                return content;
            }
        }

        private string AddLinkedResources(string email)
        {
            email = email.Replace("{{prysm-logo}}", $"cid:{_prysmLogo.ContentId}");
            email = email.Replace("{{facebook-icon}}", $"cid:{_facebookIcon.ContentId}");
            email = email.Replace("{{google-plus-icon}}", $"cid:{_googlePlusIcon.ContentId}");
            email = email.Replace("{{linkedin-icon}}", $"cid:{_linkedInIcon.ContentId}");
            email = email.Replace("{{twitter-icon}}", $"cid:{_twitterIcon.ContentId}");
            email = email.Replace("{{youtube-icon}}", $"cid:{_youtubeIcon.ContentId}");
            return email;
        }
    }
}