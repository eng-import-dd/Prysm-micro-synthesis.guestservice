using Synthesis.GuestService.ApiWrappers.Responses;

namespace Synthesis.GuestService.Responses
{
    public class GuestCreationResponse
    {
        public CreateGuestResponseCode ResultCode { get; set; }
        public UserResponse SynthesisUser { get; set; }
    }
}