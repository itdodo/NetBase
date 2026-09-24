using System.Linq.Expressions;
using NetBase.Common.Exceptions;
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

    /// <summary>
    /// 编辑统一入口：乐观锁并发校验（版本不匹配抛业务异常）+ 字段级变更审计
    /// （sys_change_log，Db:EnableChangeAudit=false 时自动退化为纯并发校验）。
    /// </summary>
    protected async Task UpdateWithConcurrencyCheckAsync(T entity)
    {
        if (!await Repository.UpdateWithAuditAsync(entity))
        {
            throw new NetBase.Common.Exceptions.BusinessException(
                "数据已被他人修改，请刷新后重试",
                ApiResultCode.Conflict,
                ErrorCodes.COMMON_CONCURRENCY_CONFLICT);
        }
    }

    public Task<bool> UpdateAsync(T entity) => Repository.UpdateAsync(entity);

    /// <summary>
    /// 按主键取实体，不存在时抛 NotFound 业务异常（通用模式收敛：各服务不再各自实现）。
    /// </summary>
    protected async Task<T> GetRequiredAsync(long id, string notFoundMessage, string? errorCode = null) =>
        await Repository.GetByIdAsync(id)
        ?? throw new BusinessException(notFoundMessage, ApiResultCode.NotFound, errorCode ?? ErrorCodes.COMMON_NOT_FOUND);

    public bool Delete(long id) => Repository.Delete(id);

    public Task<bool> DeleteAsync(long id) => Repository.DeleteAsync(id);
}
