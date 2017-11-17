using System;
using System.Threading.Tasks;
using Synthesis.GuestService.ApiWrappers.Responses;
using Synthesis.GuestService.Models;
using Synthesis.Http.Microservice;

namespace Synthesis.GuestService.ApiWrappers.Interfaces
{
    public interface IProjectApiWrapper
    {
        Task<MicroserviceResponse<Project>> GetProjectByAccessCodeAsync(string projectAccessCode);
        Task<MicroserviceResponse<Project>> GetProjectByIdAsync(Guid projectId);
    }
}