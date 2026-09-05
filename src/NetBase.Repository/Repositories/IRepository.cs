using System.Linq.Expressions;
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

    /// <summary>按主键查询</summary>
    T? GetById(long id);

    Task<T?> GetByIdAsync(long id);

    /// <summary>按条件查询第一条，无条件时返回表的第一条</summary>
    T? GetFirst(Expression<Func<T, bool>>? predicate = null);

    Task<T?> GetFirstAsync(Expression<Func<T, bool>>? predicate = null);

    /// <summary>按条件查询列表，条件为空时返回全部</summary>
    List<T> GetList(Expression<Func<T, bool>>? predicate = null);

    Task<List<T>> GetListAsync(Expression<Func<T, bool>>? predicate = null);

    /// <summary>分页查询（返回总记录数）</summary>
    PageResult<T> GetPageList(Expression<Func<T, bool>>? predicate, PageQuery page);

    Task<PageResult<T>> GetPageListAsync(Expression<Func<T, bool>>? predicate, PageQuery page);

    /// <summary>记录数</summary>
    long Count(Expression<Func<T, bool>>? predicate = null);

    Task<long> CountAsync(Expression<Func<T, bool>>? predicate = null);

    /// <summary>是否存在</summary>
    bool Any(Expression<Func<T, bool>>? predicate = null);

    Task<bool> AnyAsync(Expression<Func<T, bool>>? predicate = null);

    #endregion

    #region 写入

    /// <summary>插入并回填自增主键</summary>
    T Insert(T entity);

    Task<T> InsertAsync(T entity);

    /// <summary>批量插入</summary>
    int InsertRange(IEnumerable<T> entities);

    Task<int> InsertRangeAsync(IEnumerable<T> entities);

    /// <summary>更新整条实体（按主键）</summary>
    bool Update(T entity);

    Task<bool> UpdateAsync(T entity);

    /// <summary>按条件批量更新列</summary>
    int UpdateWhere(Expression<Func<T, bool>> predicate, Expression<Func<T, T>> updateExpression);

    Task<int> UpdateWhereAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, T>> updateExpression);

    #endregion

    #region 删除

    /// <summary>按主键删除（实体实现 ISoftDelete 时为软删除）</summary>
    bool Delete(long id);

    Task<bool> DeleteAsync(long id);

    /// <summary>按实体删除</summary>
    bool Delete(T entity);

    /// <summary>按实体异步删除（实体实现 ISoftDelete 时为软删除）</summary>
    Task<bool> DeleteAsync(T entity);

    /// <summary>按条件删除</summary>
    int DeleteWhere(Expression<Func<T, bool>> predicate);

    Task<int> DeleteWhereAsync(Expression<Func<T, bool>> predicate);

    /// <summary>按条件物理删除（忽略软删除策略，用于日志/会话等无审计价值的表）</summary>
    int DeletePhysicalWhere(Expression<Func<T, bool>> predicate);

    Task<int> DeletePhysicalWhereAsync(Expression<Func<T, bool>> predicate);

    #endregion

    /// <summary>开启事务执行（同连接内多个操作原子提交，异常时回滚并原样抛出）</summary>
    TResult Transaction<TResult>(Func<TResult> action);

    /// <summary>开启异步事务执行（同连接内多个操作原子提交，异常时回滚并原样抛出）</summary>
    Task<TResult> TransactionAsync<TResult>(Func<Task<TResult>> action);
}
