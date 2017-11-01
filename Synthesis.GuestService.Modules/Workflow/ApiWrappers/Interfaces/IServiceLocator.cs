namespace Synthesis.GuestService.Workflow.ApiWrappers
{
    public interface IServiceLocator
    {
        string ParticipantUrl { get; }
        string ProjectUrl { get; }
        string SettingsUrl { get; }
        string UserUrl { get; }
    }
}