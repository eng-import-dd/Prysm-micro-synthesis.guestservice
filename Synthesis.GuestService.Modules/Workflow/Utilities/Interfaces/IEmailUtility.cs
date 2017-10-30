using System.Collections.Generic;
using System.Net.Mail;

namespace Synthesis.GuestService.Workflow.Utilities
{
    public interface IEmailUtility
    {
        bool SendGuestInvite(string projectName, string projectCode, string guestEmail, string from);

        bool SendResetPasswordEmail(string email, string name, string link);

        bool SendVerifyAccountEmail(string firstName, string email, string accessCode, string emailVerificationId);

        bool SendHostEmail(string email, string userFullName, string userFirstName, string userEmail, string projectName);

        bool SendContent(IEnumerable<string> emailAddresses, IEnumerable<Attachment> attachments, string fromFullName);

        bool SendUserInvite(List<InvitedUserDto> newInvitedUsers);

        bool SendWelcomeEmail(string email, string firstname);

        bool SendUserLockedMail(List<SynthesisUserBasicDto> orgAdmins, string userfullname, string useremail);
    }
}
