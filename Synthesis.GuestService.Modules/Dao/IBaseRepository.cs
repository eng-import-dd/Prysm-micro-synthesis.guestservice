using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;

namespace Synthesis.GuestService.Dao
{
    public interface IBaseRepository<T> : IDisposable
    {
        Task<T> GetItemAsync(string id);
        Task<IEnumerable<T>> GetItemsAsync(Expression<Func<T, bool>> predicate);
        Task<T> CreateItemAsync(T item);
        Task<T> UpdateItemAsync(string id, T item);
        Task DeleteItemAsync(string id);
        Task InitializeAsync();
    }
}
