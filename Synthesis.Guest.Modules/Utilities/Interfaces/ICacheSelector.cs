using Synthesis.Cache;
using Synthesis.GuestService.Enumerations;

namespace Synthesis.GuestService.Utilities.Interfaces
{
    public interface ICacheSelector
    {
        ICache this[CacheConnection connection] { get; }
    }
}
