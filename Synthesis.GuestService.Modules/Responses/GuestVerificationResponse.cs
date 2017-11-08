﻿using System;
using Synthesis.GuestService.Workflow.ApiWrappers;

namespace Synthesis.GuestService.Responses
{
    public class GuestVerificationResponse
    {
        public Guid? AccountId { get; set; }
        public ProjectResponse AssociatedProject { get; set; }
        public string ProjectAccessCode { get; set; }
        public string ProjectName { get; set; }
        public VerifyGuestResponseCode ResultCode { get; set; }
        public Guid? UserId { get; set; }
        public string Username { get; set; }
    }
}