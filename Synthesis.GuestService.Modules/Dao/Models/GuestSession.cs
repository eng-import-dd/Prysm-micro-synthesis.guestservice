using System;
using Newtonsoft.Json;


namespace Synthesis.GuestService.Dao.Models
{
    public class GuestSession
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public Guid ProjectId { get; set; }

        public string ProjectAccessCode { get; set; }

        public DateTime? CreatedDateTime { get; set; }

        public DateTime? AccessGrantedDateTime { get; set; }

        public Guid AccessGrantedBy { get; set; }

        public DateTime? AccessRevokedDateTime { get; set; }

        public Guid AccessRevokedBy { get; set; }

        public DateTime? EmailedHostDateTime { get; set; }

        public DateTime? LastAccessDate { get; set; }

    }
}
