﻿using System;
using System.Threading.Tasks;
using Synthesis.GuestService.InternalApi.Models;

namespace Synthesis.GuestService.Controllers
{
    public interface IProjectLobbyStateController
    {
        Task CreateProjectLobbyStateAsync(Guid projectId);
        Task<ProjectLobbyState> RecalculateProjectLobbyStateAsync(Guid projectId);
        Task<ProjectLobbyState> GetProjectLobbyStateAsync(Guid projectId);
        Task DeleteProjectLobbyStateAsync(Guid projectId);
        Task<ProjectLobbyState> UpsertProjectLobbyStateAsync(Guid projectId, ProjectLobbyState projectLobbyState);
    }
}
