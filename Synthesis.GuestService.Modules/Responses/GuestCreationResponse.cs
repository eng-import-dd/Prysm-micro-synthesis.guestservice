using Synthesis.GuestService.Workflow.ServiceInterop;

namespace Synthesis.GuestService.Responses
{
    public class GuestCreationResponse
    {
        public CreateGuestResponseCode ResultCode { get; set; }
        public User SynthesisUser { get; set; }
    }

    
}
