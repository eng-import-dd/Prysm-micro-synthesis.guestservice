namespace Synthesis.GuestService.Dao.Models
{
    public class GuestVerificationEmailResponse : GuestVerificationEmailRequest
    {
        public SendVerificationResult SendVerificationStatus { get; set; }
    }
}