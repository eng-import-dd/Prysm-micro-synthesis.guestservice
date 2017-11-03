namespace Synthesis.GuestService.Workflow.ApiWrappers
{
    public class GuestVerificationEmailResponse : GuestVerificationEmailRequest
    {
        public bool HasMsgAlreadySentWithinLastMinute { get; set; }

        public bool IsEmailVerified { get; set; }
    }
}