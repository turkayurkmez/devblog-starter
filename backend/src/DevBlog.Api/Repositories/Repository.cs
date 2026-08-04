using System.Linq.Expressions;
using DevBlog.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DevBlog.Api.Repositories;

public class Repository<T>(AppDbContext db) : IRepository<T> where T : class
{
    protected AppDbContext Db => db;

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate) =>
        db.Set<T>().AnyAsync(predicate);

    public async Task AddAsync(T entity) =>
        await db.Set<T>().AddAsync(entity);

    public Task SaveChangesAsync() =>
        db.SaveChangesAsync();
}
