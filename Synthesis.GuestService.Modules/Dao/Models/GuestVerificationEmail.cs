namespace Synthesis.GuestService.Dao.Models
{
    public class GuestVerificationEmail
    {
        public string ProjectAccessCode { get; set; }

        public string Email { get; set; }

        public SendVerificationResult SendVerificationStatus { get; set; }
    }

    public enum SendVerificationResult
    {
        Success,
        MsgAlreadySentWithinLastMinute,
        EmailNotVerified,
        FailedToSend
    }
}
