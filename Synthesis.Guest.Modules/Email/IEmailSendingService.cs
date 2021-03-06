﻿using System.Threading.Tasks;
using Synthesis.Http.Microservice;

namespace Synthesis.GuestService.Email
{
    public interface IEmailSendingService
    {
        Task<MicroserviceResponse> SendGuestInviteEmailAsync(string projectName, string projectUri, string guestEmail, string fromFirstName);
        Task<MicroserviceResponse> SendNotifyHostEmailAsync(string hostEmail, string projectUri, string projectName, string guestFullName, string guestEmail, string guestFirstName);
    }
}
