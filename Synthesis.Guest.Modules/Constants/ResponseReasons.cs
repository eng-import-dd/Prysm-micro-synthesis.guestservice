namespace Synthesis.GuestService.Constants
{
    public enum DocumentDbCodes
    {
        NotFound
    }

    public class ResponseReasons
    {
        // -- Generic
        public const string FailedToBindToRequest = "An error occured while trying to bind the payload to the request";
        // -- GuestInvite
        // Internal Server Errors
        public const string InternalServerErrorCreateGuestInvite = "An error occurred while creating the GuestInvite";
        public const string InternalServerErrorGetGuestInvite = "An error occurred retrieving the GuestInvite";
        public const string InternalServerErrorGetGuestInvites = "An error occurred retrieving the GuestInvite";
        public const string InternalServerErrorUpdateGuestInvite = "An error occurred updating the GuestInvite";
        public const string InternalServerErrorGetProjectLobbyState = "An error occurred retrieving the ProjectLobbyState";

        // Not Found Errors
        public const string NotFoundGuestInvite = "GuestInvite Not Found";
        public const string NotFoundProjectLobbyState = "ProjectLobbyState Not Found";

        // -- GuestSession
        // Internal Server Errors
        public const string InternalServerErrorCreateGuestSession = "An error occurred while creating the GuestSession";
        public const string InternalServerErrorGetGuestSession = "An error occurred retrieving the GuestSession";
        public const string InternalServerErrorGetGuestSessions = "An error occurred retrieving the GuestSession";
        public const string InternalServerErrorUpdateGuestSession = "An error occurred updating the GuestSession";
        // Not Found Errors
        public const string NotFoundGuestSession = "GuestSession Not Found";
    }
}