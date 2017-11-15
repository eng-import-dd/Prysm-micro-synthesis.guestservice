using System;
using System.Runtime.Serialization;
using Microsoft.Hadoop.Avro;
using Newtonsoft.Json;

namespace Synthesis.GuestService.Models
{
    [DataContract]
    public class GuestInvite
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [DataMember]
        public DateTime? CreatedDateTime { get; set; }

        [DataMember]
        [NullableSchema]
        public string FirstName { get; set; }

        [DataMember]
        [NullableSchema]
        public DateTime? GuestAccessCodeCreatedDateTime { get; set; }

        [DataMember]
        public string GuestEmail { get; set; }

        [DataMember]
        public Guid InvitedBy { get; set; }

        [DataMember]
        [NullableSchema]
        public DateTime? LastAccessDate { get; set; }

        [DataMember]
        [NullableSchema]
        public string LastName { get; set; }

        [DataMember]
        [NullableSchema]
        public string ProjectAccessCode { get; set; }

        [DataMember]
        public Guid ProjectId { get; set; }

        [DataMember]
        public Guid UserId { get; set; }
    }
}