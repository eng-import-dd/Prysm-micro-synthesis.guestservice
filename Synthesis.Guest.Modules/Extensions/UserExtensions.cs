
using Synthesis.PrincipalService.InternalApi.Models;

namespace Synthesis.GuestService.Extensions
{
    public static class UserExtensions
    {
        public static string GetFullName(this User user)
        {
            return $"{user.FirstName} {user.LastName}";
        }
    }
}
