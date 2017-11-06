using System;
using System.Threading.Tasks;
using Synthesis.GuestService.Workflow.ServiceInterop.Responses;

namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public interface ISettingsInterop
    {
        // TODO: Implement these methods
        Task<UserSettings> GetUserSettingsAsync(Guid projectAccountId);
    }
}