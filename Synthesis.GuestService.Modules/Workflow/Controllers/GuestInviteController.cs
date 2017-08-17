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
    public class GuestInviteController : IGuestInviteController
    {
        private readonly IBaseRepository<GuestInvite> _guestInviteRepository;

        public GuestInviteController(IRepositoryFactory repositoryFactory)
        {
            _guestInviteRepository = repositoryFactory.CreateRepository<GuestInvite>();
        }

        // -- "Create" Methods
        public async Task<GuestInvite> CreateGuestInvite(GuestInvite request)
        {
            try
            {
                var guestInvite = new GuestInvite
                {
                    GuestInviteId = request.GuestInviteId,
                    InvitedBy = request.InvitedBy,
                    ProjectId = request.ProjectId,
                    GuestEmail = request.GuestEmail,
                    CreatedDateTime = request.CreatedDateTime,
                    ProjectAccessCode = request.ProjectAccessCode
                };

                return await _guestInviteRepository.CreateItemAsync(guestInvite);
            }
            catch (Exception e)
            {
                throw new NotImplementedException(e.Message);
            }
        }

        // -- "Read" Methods
        public async Task<GuestInvite> GetGuestInviteByIdAsync(Guid guestInviteId)
        {
            try
            {
                return await _guestInviteRepository.GetItemAsync(guestInviteId.ToString());
            }
            catch (Exception e)
            {
                throw new NotImplementedException(e.Message);
            }
        }
        public async Task<IEnumerable<GuestInvite>> GetGuestInvitesByEmailAsync(string guestEmail)
        {
            try
            {
                return await _guestInviteRepository.GetItemsAsync(gi => gi.GuestEmail == guestEmail);
            }
            catch (Exception e)
            {
                throw new NotImplementedException(e.Message);
            }
        }
        public async Task<IEnumerable<GuestInvite>> GetGuestInvitesByProjectId(Guid projectId)
        {
            try
            {
                return await _guestInviteRepository.GetItemsAsync(gi => gi.ProjectId == projectId);
            }
            catch (Exception e)
            {
                throw new NotImplementedException(e.Message);
            }
        }

        // -- "Update" Methods
        public async Task<GuestInvite> UpdateGuestInviteAsync(GuestInvite request)
        {
            try
            {
                return await _guestInviteRepository.UpdateItemAsync(request.GuestInviteId.ToString(), request);
            }
            catch (Exception e)
            {
                throw new NotImplementedException(e.Message);
            }
        }

        // -- "Delete" Methods
        // No delete operations for GuestInvite at this time
    }
}