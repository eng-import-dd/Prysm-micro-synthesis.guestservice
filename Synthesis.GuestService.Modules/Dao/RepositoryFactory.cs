using Synthesis.GuestService.Modules.Dao.Interfaces;

namespace Synthesis.GuestService.Modules.Dao
{
    public class RepositoryFactory : IRepositoryFactory
    {
        public IBaseRepository<T> CreateRepository<T>() where T : class
        {
            return new DocumentDbRepository<T>();
        }
    }
}