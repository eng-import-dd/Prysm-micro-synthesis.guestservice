using System.Threading.Tasks;
using Synthesis.Http.Microservice;

namespace Synthesis.GuestService.Email
{
    public interface IEmailSendingService
    {
        Task<MicroserviceResponse> SendGuestInviteEmailAsync(string projectName, string projectCode, string guestEmail, string fromFirstName);
    }
}
