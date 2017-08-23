using System;
using Newtonsoft.Json;


namespace Synthesis.GuestService.Dao.Models
{
    public class GuestInvite
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        public Guid InvitedBy { get; set; }

        public Guid ProjectId { get; set; }

        public string GuestEmail { get; set; }

        public string ProjectAccessCode { get; set; }

        public DateTime? CreatedDateTime { get; set; }

        public DateTime? LastAccessDate { get; set; }

    }
}
