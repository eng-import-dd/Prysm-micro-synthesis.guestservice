namespace Synthesis.GuestService.Requests
{
    public class GuestCreationRequest
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string PasswordConfirmation { get; set; }
        public string ProjectAccessCode { get; set; }
        public bool IsIdpUser { get; set; }
    }
}
