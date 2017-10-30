namespace Synthesis.GuestService.Responses
{
    public enum CreateGuestResponseCode
    {
        Failed,
        Unauthorized,
        FirstOrLastNameIsNull,
        EmailIsNotUnique,
        InvalidEmail,
        UserExists,
        UsernameIsNotUnique,
        InvalidPassword,
        PasswordConfirmationError,
        SucessEmailVerificationNeeded,
        Success
    }
}