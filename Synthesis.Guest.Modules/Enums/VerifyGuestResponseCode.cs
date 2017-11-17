namespace Synthesis.GuestService.Enums
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