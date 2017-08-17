using System.Runtime.Serialization;

namespace Synthesis.GuestService.Modules.Dto
{
    public class UserSettingsDto
    {
        [DataMember]
        public bool EnableBoxForProjectFiles { get; set; }

        [DataMember]
        public bool EnableOneDriveForBusinessForProjectFiles { get; set; }

        [DataMember]
        public bool IsGuestModeEnabled { get; set; }

        [DataMember]
        public bool EnableDropboxForProjectFiles { get; set; }

        [DataMember]
        public bool EnableLiveSourceStreaming { get; set; }

        [DataMember]
        public bool EnableGoogleDriveForProjectFiles { get; set; }

        [DataMember]
        public int CobrowserWorkspaceCountLimit { get; set; }
    }
}
