
namespace Synthesis.GuestService.Modules.Dao.Interfaces
{
    public interface IRepositoryFactory
    {
        IBaseRepository<T> CreateRepository<T>() where T : class;
    }
}