using System.Linq.Expressions;

namespace IssueFlow.Repositories.Interfaces
{
    /// <summary>
    /// Generic repository abstraction over EF Core for a given entity type.
    /// Keeps data-access concerns out of controllers/services.
    /// </summary>
    public interface IGenericRepository<T> where T : class
    {
        Task<T?> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        IQueryable<T> Query();
        Task AddAsync(T entity);
        void Update(T entity);
        void Remove(T entity);
        Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate);
    }
}
