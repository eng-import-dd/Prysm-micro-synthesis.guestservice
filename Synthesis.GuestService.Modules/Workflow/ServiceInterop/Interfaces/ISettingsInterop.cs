using System;
using System.Threading.Tasks;
using Synthesis.GuestService.Workflow.ServiceInterop.Responses;

namespace Synthesis.GuestService.Workflow.ServiceInterop
{
    public interface ISettingsInterop
    {
        Task<UserSettings> GetUserSettingsAsync(Guid projectAccountId);
    }
}