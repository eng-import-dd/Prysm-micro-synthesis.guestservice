namespace Synthesis.GuestService.Constants
{
    public class Routing
    {
        // Base Routes
        public const string GuestHealthCheckRoute = "/v1/health";
        public const string GuestsRoute = "/v1/guests";
        public const string GuestInvitesRoute = "/v1/" + GuestInvitesPath;
        public const string GuestSessionsRoute = "/v1/" + GuestSessionsPath;
        public const string ProjectsRoute = "/v1/projects";
        public const string VerifyGuestRoute = "/v1/verify";

        // Paths
        public const string ProjectLobbyStatePath = "lobbystate";
        public const string GuestSessionsPath = "guestsessions";
        public const string GuestInvitesPath = "guestinvites";
        public const string VerifyGuestPath = "verify";
        public const string VerificationEmailPath = "verificationemail";
        public const string EmailHostPath = "emailhost";
    }
}
