
namespace Synthesis.GuestService.Dao
{
    public interface IRepositoryFactory
    {
        IBaseRepository<T> CreateRepository<T>() where T : class;
    }
}