using System;

namespace Synthesis.GuestService.Dao.Models
{
    public class Guest
    {
        public GuestInvite Invite { get; set; }

        public GuestSession Session { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Email { get; set; }

        public string Password { get; set; }

        public string PasswordConfirmation { get; set; }

        public string ProjectAccessCode { get; set; }
    }
}
