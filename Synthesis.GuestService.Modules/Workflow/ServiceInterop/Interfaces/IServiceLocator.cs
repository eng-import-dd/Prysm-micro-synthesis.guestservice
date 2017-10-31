namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public interface IServiceLocator
    {
        string ParticipantUrl { get; }
        string ProjectUrl { get; }
        string SettingsUrl { get; }
        string TenantUrl { get; }
        string UserUrl { get; }
    }
}