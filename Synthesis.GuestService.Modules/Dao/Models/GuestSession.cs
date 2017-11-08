using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Synthesis.GuestService.Dao.Models
{
    public class GuestSession
    {
        public Guid AccessGrantedBy { get; set; }
        public DateTime? AccessGrantedDateTime { get; set; }
        public Guid AccessRevokedBy { get; set; }
        public DateTime? AccessRevokedDateTime { get; set; }
        public DateTime? CreatedDateTime { get; set; }
        public string Email { get; set; }
        public DateTime? EmailedHostDateTime { get; set; }
        public string FirstName { get; set; }
        public GuestState GuestSessionState { get; set; }

        [JsonProperty("id")]
        public Guid Id { get; set; }

        public DateTime? LastAccessDate { get; set; }
        public string LastName { get; set; }
        public string ProjectAccessCode { get; set; }
        public Guid ProjectId { get; set; }
        public Guid UserId { get; set; }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum GuestState
    {
        InLobby = 0,
        InProject = 1,
        Ended = 2
    }
}