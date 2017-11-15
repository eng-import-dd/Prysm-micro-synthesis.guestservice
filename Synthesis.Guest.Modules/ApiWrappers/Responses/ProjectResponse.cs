using System;

namespace Synthesis.GuestService.ApiWrappers.Responses
{
    public class ProjectResponse
    {
        public Guid AccountId { get; set; }
        public Guid? AspectRatioId { get; set; }
        public DateTime? CreatedDate { get; set; }
        public string Description { get; set; }
        public string GuestAccessCode { get; set; }
        public DateTime? GuestAccessCodeCreatedDateTime { get; set; }
        public Guid Id { get; set; }
        public bool? IsGuestModeEnabled { get; set; }
        public DateTime? LastAccessDate { get; set; }
        public string Name { get; set; }
        public Guid? OwnerId { get; set; }
        public DateTime? StartDate { get; set; }
        public Guid? TenantId { get; set; }
        public int UserCount { get; set; }
    }
}