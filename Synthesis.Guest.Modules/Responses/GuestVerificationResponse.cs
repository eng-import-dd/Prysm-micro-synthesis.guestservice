﻿using System;
using Synthesis.GuestService.Enums;
using Synthesis.GuestService.Models;

namespace Synthesis.GuestService.Responses
{
    public class GuestVerificationResponse
    {
        public Guid? AccountId { get; set; }
        public Project AssociatedProject { get; set; }
        public string ProjectAccessCode { get; set; }
        public string ProjectName { get; set; }
        public VerifyGuestResponseCode ResultCode { get; set; }
        public Guid? UserId { get; set; }
        public string Username { get; set; }
    }
}