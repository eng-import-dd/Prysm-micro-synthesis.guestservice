using System;
using System.Runtime.Serialization;
using Microsoft.Hadoop.Avro;
using Newtonsoft.Json;

namespace Synthesis.GuestService.Models
{
    [DataContract]
    public class GuestSession
    {
        [JsonProperty("id")]
        [DataMember]
        public Guid Id { get; set; }

        [DataMember]
        public Guid AccessGrantedBy { get; set; }

        [DataMember]
        [NullableSchema]
        public DateTime? AccessGrantedDateTime { get; set; }

        [DataMember]
        public Guid AccessRevokedBy { get; set; }

        [DataMember]
        [NullableSchema]
        public DateTime? AccessRevokedDateTime { get; set; }

        [DataMember]
        public DateTime? CreatedDateTime { get; set; }

        [DataMember]
        [NullableSchema]
        public string Email { get; set; }

        [DataMember]
        [NullableSchema]
        public DateTime? EmailedHostDateTime { get; set; }

        [DataMember]
        [NullableSchema]
        public string FirstName { get; set; }

        [DataMember]
        public GuestState GuestSessionState { get; set; }

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