using System;
using System.Linq;

namespace Synthesis.GuestService.Workflow.ApiWrappers
{
    public class UserResponse
    {
        public string FullName => $"{FirstName} {LastName}";

        public string Initials
        {
            get
            {
                if (!string.IsNullOrEmpty(FirstName) && !string.IsNullOrEmpty(LastName))
                {
                    return $"{FirstName?.ToUpper().FirstOrDefault()}{LastName.ToUpper().FirstOrDefault()}";
                }

                return string.Empty;
            }
        }

        public Guid? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string Email { get; set; }
        public DateTime? EmailVerifiedDateTime { get; set; }
        public string FirstName { get; set; }
        public Guid? Id { get; set; }
        public bool? IsEmailVerified { get; set; }
        public bool? IsIdpUser { get; set; }
        public bool IsLocked { get; set; }
        public DateTime? LastAccessDate { get; set; }
        public DateTime? LastLogin { get; set; }
        public string LastName { get; set; }
        public string LdapId { get; set; }
        public int? LicenseType { get; set; }
        public int? PasswordAttempts { get; set; }
        public Guid? TenantId { get; set; }
        public string UserName { get; set; }
        public DateTime? VerificationEmailSentDateTime { get; set; }
        public ProvisionGuestUserReturnCode? ProvisionReturnCode { get; set; }
    }
}