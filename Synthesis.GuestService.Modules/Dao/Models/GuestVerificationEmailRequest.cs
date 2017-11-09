namespace Synthesis.GuestService.Dao.Models
{
    public class GuestVerificationEmailRequest
    {
        public string ProjectAccessCode { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Email { get; set; }
    }
}
