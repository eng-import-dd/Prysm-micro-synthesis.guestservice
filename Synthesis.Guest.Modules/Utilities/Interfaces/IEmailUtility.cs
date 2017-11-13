namespace Synthesis.GuestService.Utilities.Interfaces
{
    public interface IEmailUtility
    {
        bool SendGuestInvite(string projectName, string projectCode, string guestEmail, string from);
        bool SendVerifyAccountEmail(string firstName, string email, string accessCode, string emailVerificationId);
        bool SendHostEmail(string email, string userFullName, string userFirstName, string userEmail, string projectName);
    }
}