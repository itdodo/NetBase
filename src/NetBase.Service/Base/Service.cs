using System.Linq.Expressions;
using NetBase.Common.Results;
using NetBase.Model.Entities;
using NetBase.Repository.Repositories;

namespace NetBase.Service.Base;

/// <summary>
/// 泛型业务服务基类，封装通用 CRUD，具体业务服务继承并扩展。
/// </summary>
public class BaseService<T> : IBaseService<T> where T : BaseEntity, new()
{
    public BaseService(IRepository<T> repository)
    {
        Repository = repository;
    }

    public IRepository<T> Repository { get; }

    public T? GetById(long id) => Repository.GetById(id);

    public Task<T?> GetByIdAsync(long id) => Repository.GetByIdAsync(id);

    public List<T> GetList(Expression<Func<T, bool>>? predicate = null) => Repository.GetList(predicate);

    public Task<List<T>> GetListAsync(Expression<Func<T, bool>>? predicate = null) => Repository.GetListAsync(predicate);

    public PageResult<T> GetPageList(Expression<Func<T, bool>>? predicate, PageQuery page) =>
        Repository.GetPageList(predicate, page);

    public Task<PageResult<T>> GetPageListAsync(Expression<Func<T, bool>>? predicate, PageQuery page) =>
        Repository.GetPageListAsync(predicate, page);

    public long Count(Expression<Func<T, bool>>? predicate = null) => Repository.Count(predicate);

    public bool Any(Expression<Func<T, bool>>? predicate = null) => Repository.Any(predicate);

    public T Insert(T entity) => Repository.Insert(entity);

    public Task<T> InsertAsync(T entity) => Repository.InsertAsync(entity);

    public bool Update(T entity) => Repository.Update(entity);

    public Task<bool> UpdateAsync(T entity) => Repository.UpdateAsync(entity);

    public bool Delete(long id) => Repository.Delete(id);

    public Task<bool> DeleteAsync(long id) => Repository.DeleteAsync(id);
}
