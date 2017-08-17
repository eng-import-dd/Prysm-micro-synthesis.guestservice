using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Synthesis.GuestService.Modules.Dto
{
    [Serializable]
    [DataContract]
    public sealed class ProjectGuestsDto
    {
        /// <summary>
        /// The list of guests that have been in the project for the current project access code.
        /// </summary>
        [DataMember]
        public List<GuestSessionDto> GuestSessions { get; set; }

        /// <summary>
        /// The list of guests that have been invited into the project.
        /// </summary>
        [DataMember]
        public List<GuestInviteDto> InvitedGuests { get; set; }
    }
}