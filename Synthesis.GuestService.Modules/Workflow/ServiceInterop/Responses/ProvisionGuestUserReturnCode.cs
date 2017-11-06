namespace Synthesis.GuestService.Workflow.ServiceInterop.Responses
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