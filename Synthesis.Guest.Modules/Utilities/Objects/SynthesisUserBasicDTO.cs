using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Synthesis.GuestService.Utilities.Objects
{
    [Serializable]
    [DataContract]
    public class SynthesisUserBasicDto
    {
        public string FullName => $"{FirstName} {LastName}";
        public string Initials => $"{FirstName.ToUpper().FirstOrDefault()}{LastName.ToUpper().FirstOrDefault()}";

        [DataMember]
        public string Email { get; set; }

        [DataMember]
        public string FirstName { get; set; }

        [DataMember]
        public string LastName { get; set; }

        [DataMember]
        public Guid UserId { get; set; }
    }
}