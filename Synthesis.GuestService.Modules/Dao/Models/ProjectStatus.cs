using System;

namespace Synthesis.GuestService.Dao.Models
{
    public class ProjectStatus
    {
        public LobbyState LobbyStatus { get; set; }
    }

    public enum LobbyState
    {
        Normal,
        GuestLimitReached,
        HostNotPresent,
        Error
    }
}
