namespace Synthesis.GuestService.Responses
{
    public enum VerifyGuestResponseCode
    {
        Success,
        SuccessNoUser,
        EmailVerificationNeeded,
        InvalidCode,
        InvalidNotGuest,
        InvalidEmail,
        UserIsLocked,
        Failed
    }
}