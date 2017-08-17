using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace Synthesis.Common
{

    [Serializable]
    [DataContract]
    public sealed class ProjectDto : BaseDTO
    {
        [DataMember]
        public Guid ProjectID { get; set; }

        private string _name;
        [DataMember]
        [Required]
        [StringLength(250)]
        public string Name
        {
            get { return _name; }
            set { if (_name != value) { _name = value; OnPropertyChanged(); } }
        }

        [DataMember]
        [StringLength(140)]
        public string Description { get; set; }

        [DataMember]
        public DateTime? DateCreated { get; set; }

        [DataMember]
        public DateTime? DateStarted { get; set; }

        [DataMember]
        public DateTime? DateLastAccessed { get; set; }

        [DataMember]
        public Guid? ManagerUserID { get; set; }

        [DataMember]
        public Guid? ProductID { get; set; }

        [DataMember]
        [Required]
        [ValidGuid]
        public Guid? AspectRatioId { get; set; }

        private int? _workspaceCount;
        [DataMember]
        public int? WorkspaceCount
        {
            get { return _workspaceCount; }
            set { if (_workspaceCount != value) { _workspaceCount = value; OnPropertyChanged(); } }
        }

        private int? _fileCount;
        [DataMember]
        public int? FileCount
        {
            get { return _fileCount; }
            set { if (_fileCount != value) { _fileCount = value; OnPropertyChanged(); } }
        }

        // A non-schema property used to determine which machines receive signalr notifications
        [DataMember]
        public string ConnectionId { get; set; }

        // A non-schema property used to determine which machines is presenting in follow me mode
        [DataMember]
        public FollowMeDTO FollowMeDto { get; set; }

        /// <summary>
        /// Populated only on requests from the client. Value is the UserId of the user logged in 
        /// to the client making the request.
        /// </summary>
        [DataMember]
        public Guid UserId { get; set; }

        /// <summary>
        /// User count only get populated when getting all projects for account
        /// </summary>
        [DataMember]
        public int UserCount { get; set; }

        /// <summary>
        /// Project Owner's name only get populated when getting all projects for account
        /// </summary>
        [DataMember]
        public string ManagerUserName { get; set; }

        [DataMember]
        public string AspectRatioName { get; set; }

        /// <summary>
        /// True if user exists in the userProject table for the project Id
        /// </summary>
        [DataMember]
        public bool UserHasAccess { get; set; }

        [DataMember]
        public bool? IsGuestModeEnabled { get; set; }

        [DataMember]
        public string GuestAccessCode { get; set; }

        [DataMember]
        public DateTime? GuestAccessCodeCreatedDateTime { get; set; }

        /// <summary>
        /// The current number of participants in the project. This includes guests.
        /// </summary>
        [DataMember]
        public int ParticipantCount { get; set; }
    }
}
