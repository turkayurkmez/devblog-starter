using System.Linq.Expressions;

namespace DevBlog.Api.Repositories;

public interface IRepository<T> where T : class
{
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);
    Task AddAsync(T entity);
    Task SaveChangesAsync();
}
