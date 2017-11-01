using System;

namespace Synthesis.GuestService.Workflow.ApiWrappers
{
    public class ParticipantResponse
    {
        public string Color { get; set; }
        public string ConnectionId { get; set; }
        public Guid GuestSessionId { get; set; }
        public Guid Id { get; set; }
        public bool IsFollowing { get; set; }
        public bool IsGuest { get; set; }
        public bool IsWebClient { get; set; }
        public string Location { get; set; }
        public float PanPercentage { get; set; }
        public Guid ProjectId { get; set; }
        public float StageHeight { get; set; }
        public float StageWidth { get; set; }
        public Guid UserId { get; set; }
        public float ViewportRatio { get; set; }
        public Guid WorkspaceId { get; set; }
    }
}