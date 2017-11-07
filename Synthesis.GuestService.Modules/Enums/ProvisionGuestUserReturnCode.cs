namespace Synthesis.GuestService
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