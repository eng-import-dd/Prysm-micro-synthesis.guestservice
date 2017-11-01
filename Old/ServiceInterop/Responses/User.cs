using System;

namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public class User
    {
        public Guid Id { get; set; }
        public bool? IsLocked { get; set; }
        public bool? IsEmailVerified { get; set; }
        public Guid? AccountId { get; set; }
        public DateTime? VerificationEmailSentDateTime { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string PasswordSalt { get; set; }
        public bool IsIdpUser { get; set; }

    }
}