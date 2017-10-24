using System;

namespace Synthesis.GuestService.Workflow.ServiceInterop.Responses
{
    public class User
    {
        public bool? IsLocked { get; set; }
        public bool? IsEmailVerified { get; set; }
        public Guid? AccountId { get; set; }
    }
}