namespace Synthesis.GuestService.Constants
{
    public enum DocumentDbCodes
    {
        NotFound
    }

    public class ResponseReasons
    {
        // Internal server errors
        public const string InternalServerErrorCreateGuestInvite = "An error occurred while creating the GuestInvite";

        public const string InternalServerErrorDeleteGuestInvite = "An error occurred deleting the GuestInvite";
        public const string InternalServerErrorGetGuestInvite = "An error occurred retrieving the GuestInvite";
        public const string InternalServerErrorGetGuestInvites = "An error occurred retrieving the GuestInvite";
        public const string InternalServerErrorUpdateGuestInvite = "An error occurred updating the GuestInvite";

        // Not found
        public const string NotFoundGuestInvite = "GuestInvite Not Found";
    }
}