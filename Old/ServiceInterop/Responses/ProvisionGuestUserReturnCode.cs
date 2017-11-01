namespace Synthesis.GuestService.Workflow.ServiceInterop
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