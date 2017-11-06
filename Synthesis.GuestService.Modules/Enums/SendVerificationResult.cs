namespace Synthesis.GuestService
{
    public enum SendVerificationResult
    {
        Success,
        MsgAlreadySentWithinLastMinute,
        EmailNotVerified,
        FailedToSend
    }
}