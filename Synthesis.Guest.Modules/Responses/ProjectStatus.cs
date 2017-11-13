using System;
using Synthesis.GuestService.Enums;

namespace Synthesis.GuestService.Responses
{
    public class ProjectStatus
    {
        public ProjectStatus()
        {
        }

        public ProjectStatus(LobbyState lobbyStatus)
        {
            LobbyStatus = lobbyStatus;
        }

        public LobbyState LobbyStatus { get; set; }
        public Guid ProjectId { get; set; }

        public static LobbyState CalculateLobbyStatus(bool isGuestLimitReached, bool isHostPresent)
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