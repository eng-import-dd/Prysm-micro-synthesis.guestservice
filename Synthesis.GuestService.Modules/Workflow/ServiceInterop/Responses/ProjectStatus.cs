using System;

namespace Synthesis.GuestService.Workflow.ServiceInterop
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

        public Guid ProjectId { get; set; }
    }

    public enum LobbyState
    {
        Normal,
        GuestLimitReached,
        HostNotPresent,
        Error
    }
}