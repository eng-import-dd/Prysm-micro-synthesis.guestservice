using Synthesis.GuestService.ApiWrappers.Requests;

namespace Synthesis.GuestService.ApiWrappers.Responses
{
    public class GuestVerificationEmailResponse : GuestVerificationEmailRequest
    {
        public bool MessageSentRecently { get; set; }
        public bool IsEmailVerified { get; set; }
    }
}