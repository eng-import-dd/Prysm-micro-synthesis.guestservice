using Autofac.Features.Indexed;
using Synthesis.Cache;
using Synthesis.GuestService.Enumerations;
using Synthesis.GuestService.Utilities.Interfaces;

namespace Synthesis.GuestService.Utilities
{
    public class CacheSelector : ICacheSelector
    {
        private readonly IIndex<CacheConnection, ICache> _cacheIndexer;

        public CacheSelector(IIndex<CacheConnection, ICache> cacheIndexer)
        {
            _cacheIndexer = cacheIndexer;
        }

        public ICache this[CacheConnection connection] => _cacheIndexer[connection];
    }
}
