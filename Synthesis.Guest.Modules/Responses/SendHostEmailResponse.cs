using System;

namespace Synthesis.GuestService.Responses
{
    public class SendHostEmailResponse
    {
        public DateTime EmailSentDateTime { get; set; }
        public string SentBy { get; set; }
    }
}