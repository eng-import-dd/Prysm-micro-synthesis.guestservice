namespace Synthesis.GuestService.Requests
{
    public class GuestVerificationRequest
    {
        private string _projectAccessCode;

        public string ProjectAccessCode
        {
            get => _projectAccessCode;
            set => _projectAccessCode = value.Replace("-", string.Empty).Replace(" ", string.Empty);
        }

        public string Username { get; set; }
    }
}