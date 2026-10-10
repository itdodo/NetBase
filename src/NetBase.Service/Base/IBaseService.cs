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


    Task<T?> GetByIdAsync(long id);


    Task<List<T>> GetListAsync(Expression<Func<T, bool>>? predicate = null);


    Task<PageResult<T>> GetPageListAsync(Expression<Func<T, bool>>? predicate, PageQuery page);




    Task<T> InsertAsync(T entity);


    Task<bool> UpdateAsync(T entity);


    Task<bool> DeleteAsync(long id);
}
