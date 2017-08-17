
namespace Synthesis.GuestService.Dao
{
    public class RepositoryFactory : IRepositoryFactory
    {
        public IBaseRepository<T> CreateRepository<T>() where T : class
        {
            return new DocumentDbRepository<T>();
        }
    }
}