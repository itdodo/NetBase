using System.Linq.Expressions;
using NetBase.Common.Results;
using NetBase.Model.Entities;
using NetBase.Repository.DbContexts;
using SqlSugar;

namespace NetBase.Repository.Repositories;

/// <summary>
/// 泛型仓储实现。软删除策略：实体实现 ISoftDelete 时走 Update 软删，否则物理删除。
/// </summary>
public class Repository<T> : IRepository<T> where T : BaseEntity, new()
{
    protected readonly SqlSugarContext Context;

    public Repository(SqlSugarContext context)
    {
        Context = context;
    }

    public ISqlSugarClient Db => Context.Client;

    public ISugarQueryable<T> Queryable => Db.Queryable<T>();

    private bool IsSoftDelete => typeof(ISoftDelete).IsAssignableFrom(typeof(T));

    #region 查询

    public T? GetById(long id) => Queryable.InSingle(id);

    public async Task<T?> GetByIdAsync(long id) => await Queryable.InSingleAsync(id);

    public T? GetFirst(Expression<Func<T, bool>>? predicate = null) =>
        (predicate == null ? Queryable : Queryable.Where(predicate)).First();

    public async Task<T?> GetFirstAsync(Expression<Func<T, bool>>? predicate = null) =>
        await (predicate == null ? Queryable : Queryable.Where(predicate)).FirstAsync();

    public List<T> GetList(Expression<Func<T, bool>>? predicate = null) =>
        (predicate == null ? Queryable : Queryable.Where(predicate)).ToList();

    public async Task<List<T>> GetListAsync(Expression<Func<T, bool>>? predicate = null) =>
        await (predicate == null ? Queryable : Queryable.Where(predicate)).ToListAsync();

    public PageResult<T> GetPageList(Expression<Func<T, bool>>? predicate, PageQuery page)
    {
        int total = 0;
        var items = (predicate == null ? Queryable : Queryable.Where(predicate))
            .OrderByDescending(x => x.Id)
            .ToPageList(page.PageIndex, page.PageSize, ref total);
        return PageResult<T>.Of(items, total, page.PageIndex, page.PageSize);
    }

    public async Task<PageResult<T>> GetPageListAsync(Expression<Func<T, bool>>? predicate, PageQuery page)
    {
        RefAsync<int> total = 0;
        var items = await (predicate == null ? Queryable : Queryable.Where(predicate))
            .OrderByDescending(x => x.Id)
            .ToPageListAsync(page.PageIndex, page.PageSize, total);
        return PageResult<T>.Of(items, total, page.PageIndex, page.PageSize);
    }

    public long Count(Expression<Func<T, bool>>? predicate = null) =>
        (predicate == null ? Queryable : Queryable.Where(predicate)).Count();

    public async Task<long> CountAsync(Expression<Func<T, bool>>? predicate = null) =>
        await (predicate == null ? Queryable : Queryable.Where(predicate)).CountAsync();

    public bool Any(Expression<Func<T, bool>>? predicate = null) => Count(predicate) > 0;

    public async Task<bool> AnyAsync(Expression<Func<T, bool>>? predicate = null) => await CountAsync(predicate) > 0;

    #endregion

    #region 写入

    public T Insert(T entity)
    {
        Db.Insertable(entity).ExecuteReturnEntity();
        return entity;
    }

    public async Task<T> InsertAsync(T entity)
    {
        await Db.Insertable(entity).ExecuteReturnEntityAsync();
        return entity;
    }

    public int InsertRange(IEnumerable<T> entities) => Db.Insertable(entities.ToList()).ExecuteCommand();

    public async Task<int> InsertRangeAsync(IEnumerable<T> entities) =>
        await Db.Insertable(entities.ToList()).ExecuteCommandAsync();

    public bool Update(T entity) => Db.Updateable(entity).ExecuteCommand() > 0;

    public async Task<bool> UpdateAsync(T entity) => await Db.Updateable(entity).ExecuteCommandAsync() > 0;

    public int UpdateWhere(Expression<Func<T, bool>> predicate, Expression<Func<T, T>> updateExpression) =>
        Db.Updateable<T>().SetColumns(updateExpression).Where(predicate).ExecuteCommand();

    public async Task<int> UpdateWhereAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, T>> updateExpression) =>
        await Db.Updateable<T>().SetColumns(updateExpression).Where(predicate).ExecuteCommandAsync();

    #endregion

    #region 删除

    public bool Delete(long id)
    {
        var entity = GetById(id);
        return entity != null && Delete(entity);
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var entity = await GetByIdAsync(id);
        return entity != null && await DeleteEntityAsync(entity);
    }

    public bool Delete(T entity) => DeleteEntity(entity);

    public int DeleteWhere(Expression<Func<T, bool>> predicate)
    {
        if (IsSoftDelete)
        {
            // 按列名更新指定列，避免泛型 MemberInit 表达式在 SqlSugar 中的翻译不确定性
            return Db.Updateable<T>()
                .SetColumns("IsDeleted", true)
                .SetColumns("UpdateTime", DateTime.Now)
                .Where(predicate)
                .ExecuteCommand();
        }

        return Db.Deleteable<T>().Where(predicate).ExecuteCommand();
    }

    public async Task<int> DeleteWhereAsync(Expression<Func<T, bool>> predicate)
    {
        if (IsSoftDelete)
        {
            return await Db.Updateable<T>()
                .SetColumns("IsDeleted", true)
                .SetColumns("UpdateTime", DateTime.Now)
                .Where(predicate)
                .ExecuteCommandAsync();
        }

        return await Db.Deleteable<T>().Where(predicate).ExecuteCommandAsync();
    }

    private bool DeleteEntity(T entity)
    {
        if (entity is ISoftDelete)
        {
            ((ISoftDelete)entity).IsDeleted = true;
            entity.UpdateTime = DateTime.Now;
            return Db.Updateable(entity).ExecuteCommand() > 0;
        }

        return Db.Deleteable(entity).ExecuteCommand() > 0;
    }

    private async Task<bool> DeleteEntityAsync(T entity)
    {
        if (entity is ISoftDelete)
        {
            ((ISoftDelete)entity).IsDeleted = true;
            entity.UpdateTime = DateTime.Now;
            return await Db.Updateable(entity).ExecuteCommandAsync() > 0;
        }

        return await Db.Deleteable(entity).ExecuteCommandAsync() > 0;
    }

    #endregion

    public TResult Transaction<TResult>(Func<TResult> action) => Db.Ado.UseTran(action).Data;
}
