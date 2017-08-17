using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using FluentValidation.Internal;
using Microsoft.Azure.Documents;
using Synthesis.GuestService.Exceptions;
using Synthesis.GuestService.Dao;
using Synthesis.GuestService.Workflow.Interfaces;
using Synthesis.GuestService.Entity;

namespace Synthesis.GuestService.Workflow.Controllers
{
    public class GuestSessionController : IGuestSessionController
    {
        private readonly IBaseRepository<GuestSession> _guestSessionRepository;

        public GuestSessionController(IRepositoryFactory repositoryFactory)
        {
            _guestSessionRepository = repositoryFactory.CreateRepository<GuestSession>();
        }

        // -- "Create" Methods
        public async Task<GuestSession> CreateGuestSession(GuestSession request)
        {
            try
            {
                var guestSession = new GuestSession
                {
                    GuestSessionId = request.GuestSessionId,
                    UserId = request.UserId,
                    ProjectId = request.ProjectId,
                    ProjectAccessCode = request.ProjectAccessCode,
                    GuestSessionStateId = request.GuestSessionStateId,
                    CreatedDateTime = request.CreatedDateTime,
                    AccessGrantedDateTime = request.AccessGrantedDateTime,
                    AccessGrantedBy = request.AccessGrantedBy,
                    AccessRevokedDateTime = request.AccessRevokedDateTime,
                    AccessRevokedBy = request.AccessRevokedBy,
                    EmailedHostDateTime = request.EmailedHostDateTime
                };

                return await _guestSessionRepository.CreateItemAsync(guestSession);
            }
            catch (Exception e)
            {
                throw new NotImplementedException(e.Message);
            }
        }

        // -- "Read" Methods
        public async Task<GuestSession> GetGuestSessionByIdAsync(Guid guestSessionId)
        {
            try
            {
                return await _guestSessionRepository.GetItemAsync(guestSessionId.ToString());
            }
            catch (Exception e)
            {
                throw new NotImplementedException(e.Message);
            }
        }
        public async Task<IEnumerable<GuestSession>> GetGuestSessionsByUserId(Guid userId)
        {
            try
            {
                return await _guestSessionRepository.GetItemsAsync(gs => gs.UserId == userId);
            }
            catch (Exception e)
            {
                throw new NotImplementedException(e.Message);
            }
        }
        public async Task<IEnumerable<GuestSession>> GetGuestSessionsByProjectId(Guid projectId)
        {
            try
            {
                return await _guestSessionRepository.GetItemsAsync(gs => gs.ProjectId == projectId);
            }
            catch (Exception e)
            {
                throw new NotImplementedException(e.Message);
            }
        }

        // -- "Update" Methods
        public async Task<GuestSession> UpdateGuestSession(GuestSession request)
        {
            try
            {
                return await _guestSessionRepository.UpdateItemAsync(request.GuestSessionId.ToString(), request);
            }
            catch (Exception e)
            {
                throw new NotImplementedException(e.Message);
            }
        }

        // -- "Delete" Methods
        // No delete operations for GuestSession at this time
    }
}