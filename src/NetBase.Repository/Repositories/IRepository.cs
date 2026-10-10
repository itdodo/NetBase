using System.Linq.Expressions;
using System.Threading;
using NetBase.Common.Results;
using NetBase.Model.Entities;
using SqlSugar;

namespace NetBase.Repository.Repositories;

/// <summary>
/// 泛型仓储接口，覆盖中小项目常规 CRUD 与查询场景；
/// 复杂查询可通过 Queryable 获取 ISugarQueryable&lt;T&gt; 自行组装。
/// </summary>
public interface IRepository<T> where T : BaseEntity, new()
{
    /// <summary>获取 SqlSugar 可查询对象，供复杂查询使用</summary>
    ISugarQueryable<T> Queryable { get; }

    /// <summary>数据库客户端（用于跨表事务等场景）</summary>
    ISqlSugarClient Db { get; }

    #region 查询

    Task<T?> GetByIdAsync(long id, CancellationToken ct = default);

    Task<T?> GetFirstAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);

    Task<List<T>> GetListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);

    Task<PageResult<T>> GetPageListAsync(Expression<Func<T, bool>>? predicate, PageQuery page, CancellationToken ct = default);

    Task<long> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);

    Task<bool> AnyAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);

    #endregion

    #region 写入

    Task<T> InsertAsync(T entity, CancellationToken ct = default);

    Task<int> InsertRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

    /// <summary>
    /// 乐观锁更新：实体 Version 必须为当前库中版本，冲突（他人已先修改）返回 false。
    /// </summary>
    Task<bool> UpdateWithVersionCheckAsync(T entity, CancellationToken ct = default);

    /// <summary>乐观锁更新 + 字段级变更审计（sys_change_log）：冲突返回 false 不落审计；操作人自动取当前登录态</summary>
    Task<bool> UpdateWithAuditAsync(T entity, CancellationToken ct = default);

    Task<bool> UpdateAsync(T entity, CancellationToken ct = default);

    Task<int> UpdateWhereAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, T>> updateExpression, CancellationToken ct = default);

    #endregion

    #region 删除

    Task<bool> DeleteAsync(long id, CancellationToken ct = default);

    /// <summary>按实体异步删除（实体实现 ISoftDelete 时为软删除）</summary>
    Task<bool> DeleteAsync(T entity, CancellationToken ct = default);

    Task<int> DeleteWhereAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    Task<int> DeletePhysicalWhereAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    #endregion

    /// <summary>开启异步事务执行（同连接内多个操作原子提交，异常时回滚并原样抛出）</summary>
    Task<TResult> TransactionAsync<TResult>(Func<Task<TResult>> action, CancellationToken ct = default);
}
