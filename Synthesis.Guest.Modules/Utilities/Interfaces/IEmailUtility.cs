namespace Synthesis.GuestService.Utilities.Interfaces
{
    public interface IEmailUtility
    {
        bool SendHostEmail(string email, string userFullName, string userFirstName, string userEmail, string projectName);
    }
}