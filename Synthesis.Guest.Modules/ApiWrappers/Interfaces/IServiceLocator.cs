namespace Synthesis.GuestService.ApiWrappers.Interfaces
{
    public interface IServiceLocator
    {
        string ParticipantUrl { get; }
        string ProjectUrl { get; }
        string SettingsUrl { get; }
        string UserUrl { get; }
    }
}