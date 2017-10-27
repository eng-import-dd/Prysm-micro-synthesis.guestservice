using Synthesis.GuestService.Workflow.ServiceInterop.Responses;

namespace Synthesis.GuestService.Responses
{
    public class GuestCreationResponse
    {
        public CreateGuestResponseCode ResultCode { get; set; }
        public User SynthesisUser { get; set; }
    }

    public enum CreateGuestResponseCode
    {
        Failed,
        Unauthorized,
        FirstOrLastNameIsNull,
        InvalidEmail,
        UserExists,
        UsernameIsNotUnique,
        InvalidPassword,
        PasswordConfirmationError,
        SucessEmailVerificationNeeded,
        Success
    }
}
