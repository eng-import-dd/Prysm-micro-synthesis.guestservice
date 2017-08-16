using Newtonsoft.Json;
using System;

namespace Synthesis.GuestService.Modules.Entity
{
    public class GuestInvite
    {
        [JsonProperty("id")]
        public Guid GuestInviteId { get; set; }

        public Guid InvitedBy { get; set; }

        public Guid ProjectId { get; set; }

        public string GuestEmail { get; set; }

        public DateTime CreatedDateTime { get; set; }

        public string ProjectAccessCode { get; set; }
    }
}
