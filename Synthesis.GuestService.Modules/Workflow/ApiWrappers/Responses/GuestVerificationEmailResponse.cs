namespace Synthesis.GuestService.Workflow.ApiWrappers
{
    public class GuestVerificationEmailResponse : GuestVerificationEmailRequest
    {
        public bool MessageSentRecently { get; set; }
        public bool IsEmailVerified { get; set; }
    }
}