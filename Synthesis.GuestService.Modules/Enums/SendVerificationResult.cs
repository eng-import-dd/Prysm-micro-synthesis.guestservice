namespace Synthesis.GuestService
{
    public enum SendVerificationResult
    {
        Success,
        MessageSentRecently,
        EmailNotVerified,
        FailedToSend
    }
}