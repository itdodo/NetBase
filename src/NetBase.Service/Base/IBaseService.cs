using System.Linq.Expressions;
using NetBase.Common.Results;
using NetBase.Model.Entities;
using NetBase.Repository.Repositories;

namespace NetBase.Service.Base;

/// <summary>
/// 泛型业务服务接口。业务服务继承此接口以获得通用 CRUD 能力，
/// 再按需扩展各自的业务方法。
/// </summary>
public interface IBaseService<T> where T : BaseEntity, new()
{
    IRepository<T> Repository { get; }

    T? GetById(long id);

    Task<T?> GetByIdAsync(long id);

    List<T> GetList(Expression<Func<T, bool>>? predicate = null);

    Task<List<T>> GetListAsync(Expression<Func<T, bool>>? predicate = null);

    PageResult<T> GetPageList(Expression<Func<T, bool>>? predicate, PageQuery page);

    Task<PageResult<T>> GetPageListAsync(Expression<Func<T, bool>>? predicate, PageQuery page);

    long Count(Expression<Func<T, bool>>? predicate = null);

    bool Any(Expression<Func<T, bool>>? predicate = null);

    T Insert(T entity);

    Task<T> InsertAsync(T entity);

    bool Update(T entity);

    Task<bool> UpdateAsync(T entity);

    bool Delete(long id);

    Task<bool> DeleteAsync(long id);
}
