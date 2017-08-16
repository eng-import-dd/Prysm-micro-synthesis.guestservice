using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Synthesis.GuestService.Modules.Exceptions;
using Synthesis.GuestService.Modules.Dao.Interfaces;
using Synthesis.GuestService.Modules.Workflow.Interfaces;
using Synthesis.GuestService.Modules.Entity;
//using Synthesis.GuestService.Modules.Requests;
//using Synthesis.GuestService.Modules.Responses;

namespace Synthesis.GuestService.Modules.Workflow.Controllers
{
    public class GuestWorkflow : IGuestWorkflow
    {
        private readonly IBaseRepository<Guest> _guestRepository;

        public GuestWorkflow(IRepositoryFactory repositoryFactory)
        {
            _guestRepository = repositoryFactory.CreateRepository<Guest>();
        }

        // -- Create

        // -- Read

        // -- Update

        // --Delete
    }
}