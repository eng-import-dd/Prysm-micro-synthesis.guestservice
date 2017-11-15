using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Synthesis.GuestService.Utilities.Objects
{
    public class InvitedUserDto
    {
        [DataMember]
        public Guid AccountId { get; set; }

        [DataMember]
        public string Email { get; set; }

        [DataMember]
        public string FirstName { get; set; }

        [DataMember]
        public bool IsDuplicateUserEmail { get; set; }

        [DataMember]
        public bool IsDuplicateUserEntry { get; set; }

        [DataMember]
        public bool IsUserEmailDomainAllowed { get; set; }

        [DataMember]
        public bool IsUserEmailDomainFree { get; set; }

        [DataMember]
        public bool IsUserEmailFormatInvalid { get; set; }

        [DataMember]
        public DateTime? LastInvitedDate { get; set; }

        [DataMember]
        public string LastName { get; set; }

        /// <summary>
        ///     Metadata for InvitedUserList
        /// </summary>
        public class InvitedUserListMetaData : PagingMetaData
        {
            public List<InvitedUserDto> InvitedUsers { get; set; }
        }
    }
}