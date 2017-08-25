using System;
using FluentValidation;

namespace Synthesis.GuestService.Validators
{
    public class GuestInviteIdValidator : GuidValidator
    {
        public GuestInviteIdValidator() : base("id")
        {

        }
    }
}