namespace Synthesis.GuestService.Constants
{
    public class BaseRoutes
    {
        public const string GuestHealthCheck = "/v1/health";
        public const string GuestHealthCheckLegacy = "/api/v1/health";

        public const string Guest = "/v1/guests";
        public const string GuestLegacy = "/api/v1/guests";

        public const string GuestInvite = "/v1/guestinvites";
        public const string GuestInviteLegacy = "/api/v1/guestinvites";

        public const string GuestSession = "/v1/guestsessions";
        public const string GuestSessionLegacy = "/api/v1/guestsessions";
    }
}
