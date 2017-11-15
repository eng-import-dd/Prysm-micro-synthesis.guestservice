namespace Synthesis.GuestService.Enums
{
    public enum ProvisionGuestUserReturnCode
    {
        Success,
        SucessEmailVerificationNeeded,
        Failed,
        EmailIsNotUnique,
        UsernameIsNotUnique
    }
}