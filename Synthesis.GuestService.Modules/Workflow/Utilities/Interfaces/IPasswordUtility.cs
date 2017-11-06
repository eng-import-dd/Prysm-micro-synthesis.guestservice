namespace Synthesis.GuestService.Workflow.Utilities
{
    public interface IPasswordUtility
    {
        void HashAndSalt(string pass, out string hash, out string salt);
    }
}