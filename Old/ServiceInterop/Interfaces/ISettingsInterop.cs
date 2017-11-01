using System;
using System.Threading.Tasks;

namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public interface ISettingsInterop
    {
        Task<SettingsInteropResponse> GetPrincipalSettingsAsync(Guid projectAccountId);
    }
}