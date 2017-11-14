using Newtonsoft.Json;
using Synthesis.GuestService.Enums;
using System;

namespace Synthesis.GuestService.Models
{
    public class ProjectLobbyState
    {
        [JsonProperty("id")]
        public Guid ProjectId { get; set; }
        public LobbyState LobbyState { get; set; }

        public static LobbyState CalculateLobbyState(bool isGuestLimitReached, bool isHostPresent)
        {
            LobbyState status;

            if (!isHostPresent)
            {
                status = LobbyState.HostNotPresent;
            }
            else if (!isGuestLimitReached)
            {
                status = LobbyState.Normal;
            }
            else
            {
                status = LobbyState.GuestLimitReached;
            }

            return status;
        }
    }
}
