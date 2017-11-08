using Synthesis.GuestService.Workflow.ApiWrappers;

namespace Synthesis.GuestService.Responses
{
    public class GuestCreationResponse
    {
        public CreateGuestResponseCode ResultCode { get; set; }
        public UserResponse SynthesisUser { get; set; }
    }
}