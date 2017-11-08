namespace Synthesis.GuestService.Workflow.Utilities
{
    public interface IPasswordUtility
    {
        string GenerateRandomPassword(int length);
    }
}