using System;

namespace Synthesis.GuestService.Workflow.ServiceInterop.Responses
{
    public class Participant
    {
        public Guid Id { get; set; }

        public string ConnectionId { get; set; }

        public Guid UserId { get; set; }

        public Guid WorkspaceId { get; set; }

        public Guid GuestSessionId { get; set; }

        public Guid ProjectId { get; set; }

        public string Location { get; set; }

        public string Color { get; set; }

        public Single ViewportRatio { get; set; }

        public Single StageWidth { get; set; }

        public Single StageHeight { get; set; }

        public Single PanPercentage { get; set; }

        public bool IsFollowing { get; set; }

        public bool IsGuest { get; set; }

        public bool IsWebClient { get; set; }
    }
}
