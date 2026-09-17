using System.Linq.Expressions;
using NetBase.Common.Results;
using NetBase.Common.Auditing;
using NetBase.Model.Entities;
using NetBase.Repository.Auditing;
using NetBase.Repository.DbContexts;
using SqlSugar;

namespace NetBase.Repository.Repositories;

/// <summary>
/// 泛型仓储实现。软删除策略：实体实现 ISoftDelete 时走 Update 软删，否则物理删除。
/// </summary>
public class Repository<T> : IRepository<T> where T : BaseEntity, new()
{
    protected readonly SqlSugarContext Context;
    private readonly IOperatorProvider? _operatorProvider;

    public Repository(SqlSugarContext context, IOperatorProvider? operatorProvider = null)
    {
        Context = context;
        _operatorProvider = operatorProvider;
        _auditEnabled = context.Options.EnableChangeAudit;
    }

    private readonly bool _auditEnabled;

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
        var items = BuildPageQuery(predicate, page)
            .ToPageList(page.PageIndex, page.PageSize, ref total);
        return PageResult<T>.Of(items, total, page.PageIndex, page.PageSize);
    }

    public async Task<PageResult<T>> GetPageListAsync(Expression<Func<T, bool>>? predicate, PageQuery page)
    {
        RefAsync<int> total = 0;
        var items = await BuildPageQuery(predicate, page)
            .ToPageListAsync(page.PageIndex, page.PageSize, total);
        return PageResult<T>.Of(items, total, page.PageIndex, page.PageSize);
    }

    /// <summary>
    /// 组装分页查询：排序字段经实体属性反射白名单校验（无效回退主键），杜绝 SQL 注入。
    /// </summary>
    private ISugarQueryable<T> BuildPageQuery(Expression<Func<T, bool>>? predicate, PageQuery page)
    {
        var queryable = predicate == null ? Queryable : Queryable.Where(predicate);
        var sortField = ResolveSortField(page.SortField);
        return page.SortDesc
            ? queryable.OrderBy($"{sortField} desc")
            : queryable.OrderBy($"{sortField} asc");
    }

    /// <summary>
    /// 校验排序列是否为实体公开属性，无效值回退 CreateTime。
    /// 不回退主键：雪花 Id 只在生成器位宽配置一致时才与时间同序，
    /// 历史数据跨配置时按 Id 排序会把新记录排到末尾（审计日志曾踩坑）。
    /// </summary>
    private static string ResolveSortField(string? sortField)
    {
        const string fallback = nameof(BaseEntity.CreateTime);

        if (string.IsNullOrWhiteSpace(sortField))
        {
            return fallback;
        }

        var name = sortField.Trim();
        var exists = typeof(T).GetProperties()
            .Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return exists ? name : fallback;
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
        // ExecuteReturnEntity/ExecuteCommand 路径的 AOP 雪花触发不一致——所有路径显式填充
        FillSnowflakeId(entity);
        Db.Insertable(entity).ExecuteReturnEntity();
        return entity;
    }

    public async Task<T> InsertAsync(T entity)
    {
        FillSnowflakeId(entity);
        await Db.Insertable(entity).ExecuteReturnEntityAsync();
        return entity;
    }

    private static void FillSnowflakeId(T entity) => FillSnowflakeId((BaseEntity)entity);

    private static void FillSnowflakeId(BaseEntity entity)
    {
        if (entity.Id == 0)
        {
            entity.Id = Yitter.IdGenerator.YitIdHelper.NextId();
        }
    }

    public int InsertRange(IEnumerable<T> entities)
    {
        // 批量插入走 UNION ALL 路径，AOP 填充不触发——显式填雪花主键
        var list = MaterializeWithSnowflakeIds(entities);
        return Db.Insertable(list).ExecuteCommand();
    }

    public async Task<int> InsertRangeAsync(IEnumerable<T> entities)
    {
        var list = MaterializeWithSnowflakeIds(entities);
        return await Db.Insertable(list).ExecuteCommandAsync();
    }

    /// <summary>
    /// 物化为列表并填充雪花主键。注意必须先 ToList 再填充：
    /// 入参可能是延迟 LINQ（每次枚举产生新对象），填充后再枚举会导致雪花丢失（集成测试捕获的历史 bug）。
    /// </summary>
    private static List<T> MaterializeWithSnowflakeIds(IEnumerable<T> entities)
    {
        var list = entities.ToList();
        foreach (var entity in list)
        {
            FillSnowflakeId(entity);
        }
        return list;
    }

    public bool Update(T entity) => Db.Updateable(entity).ExecuteCommand() > 0;

    public async Task<bool> UpdateAsync(T entity) =>
        await Db.Updateable(entity).ExecuteCommandAsync() > 0;

    /// <summary>
    /// 乐观锁更新：单条 SQL 原子完成——SET 业务列 + Version=旧值+1，WHERE 主键 + Version=旧值，
    /// 版本不匹配（他人已先修改）影响 0 行返回 false。
    /// 禁止改回 WhereColumns(Version)：它会以 Version 替换主键条件，相同版本的多行
    /// 会被同一份实体值覆盖（唯一索引冲突，无索引时批量数据损坏）——集成测试实证。
    /// </summary>
    public async Task<bool> UpdateWithVersionCheckAsync(T entity)
    {
        var expected = entity.Version;

        entity.Version = expected + 1;
        var rows = await Db.Updateable(entity)
            .Where(x => x.Id == entity.Id && x.Version == expected)
            .ExecuteCommandAsync();

        if (rows == 0)
        {
            entity.Version = expected; // 冲突：回滚内存版本
            return false;
        }
        return true;
    }

    /// <summary>
    /// 乐观锁更新 + 字段级变更审计：冲突（他人已先修改）返回 false 且不落审计；
    /// 成功且有字段差异时写 sys_change_log（操作人取 IOperatorProvider，无差异不记录）。
    /// Db:EnableChangeAudit=false 时退化为纯乐观锁更新（零额外查询开销）。
    /// </summary>
    public async Task<bool> UpdateWithAuditAsync(T entity)
    {
        string? diff = null;
        if (_auditEnabled)
        {
            var before = await Db.Queryable<T>().InSingleAsync(entity.Id);
            diff = before == null ? null : AuditDiff.Diff(before, entity);
        }

        if (!await UpdateWithVersionCheckAsync(entity))
        {
            return false;
        }

        if (diff != null)
        {
            var log = new SysChangeLog
            {
                TableName = typeof(T).Name,
                RecordId = entity.Id.ToString(),
                Changes = diff,
                UserId = _operatorProvider?.OperatorUserId ?? 0,
                UserName = _operatorProvider?.OperatorName ?? "system"
            };
            FillSnowflakeId(log); // ExecuteCommand 路径 AOP 雪花不触发
            await Db.Insertable(log).ExecuteCommandAsync();
        }
        return true;
    }

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

    public Task<bool> DeleteAsync(T entity) => DeleteEntityAsync(entity);

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

    public int DeletePhysicalWhere(Expression<Func<T, bool>> predicate) =>
        Db.Deleteable<T>().Where(predicate).ExecuteCommand();

    public Task<int> DeletePhysicalWhereAsync(Expression<Func<T, bool>> predicate) =>
        Db.Deleteable<T>().Where(predicate).ExecuteCommandAsync();

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

    public TResult Transaction<TResult>(Func<TResult> action)
    {
        // UseTran 会把异常吞进 DbResult，这里重抛以保证业务异常（如参数校验）正常向上传递且已回滚
        var result = Db.Ado.UseTran(action);
        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new Exception("事务执行失败");
        }
        return result.Data;
    }

    public async Task<TResult> TransactionAsync<TResult>(Func<Task<TResult>> action)
    {
        var result = await Db.Ado.UseTranAsync(action);
        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new Exception("事务执行失败");
        }
        return result.Data;
    }
}
