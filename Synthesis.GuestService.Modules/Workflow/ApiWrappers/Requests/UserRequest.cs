using System;

namespace Synthesis.GuestService.Workflow.ApiWrappers
{
    public class UserRequest
    {
        public string Email { get; set; }
        public string FirstName { get; set; }
        public Guid? Id { get; set; }
        public bool? IsIdpUser { get; set; }
        public string LastName { get; set; }
        public string LdapId { get; set; }
        public LicenseType LicenseType { get; set; }
        public string Password { get; set;  }
        public Guid TenantId { get; set; }
        public string UserName { get; set; }
    }
}