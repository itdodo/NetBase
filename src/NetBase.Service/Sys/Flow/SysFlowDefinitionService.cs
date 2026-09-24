using NetBase.Common.Exceptions;
using NetBase.Common.Extensions;
using NetBase.Common.Results;
using NetBase.Model.Entities;
using NetBase.Repository.Repositories;
using SqlSugar;

namespace NetBase.Service.Sys.Flow;

/// <summary>审批流程定义管理：CRUD + 启停（同编码仅一个启用版本）</summary>
public interface ISysFlowDefinitionService
{
    Task<PageResult<FlowDefinitionDto>> GetPageListAsync(FlowDefinitionQueryDto query);

    Task<FlowDefinitionDto?> GetDetailAsync(long id);

    /// <summary>创建新版本（同编码自动取最大版本+1）</summary>
    Task<long> CreateAsync(FlowDefinitionSaveDto dto);

    /// <summary>更新（仅停用态可改；启用中的先停用或另存新版本）</summary>
    Task UpdateAsync(long id, FlowDefinitionSaveDto dto);

    /// <summary>删除（启用中的不允许）</summary>
    Task DeleteAsync(long id);

    /// <summary>启用（同编码其他版本自动停用）</summary>
    Task EnableAsync(long id);

    /// <summary>停用</summary>
    Task DisableAsync(long id);

    /// <summary>校验节点 JSON 可解析且结构合法（保存前预检）</summary>
    void ValidateNodeJson(string nodeJson);

    /// <summary>分类列表（名称 + 使用中的流程数）</summary>
    Task<List<FlowCategoryDto>> GetCategoriesAsync();

    /// <summary>重命名分类（新名已存在时自动合并）</summary>
    Task RenameCategoryAsync(string oldName, string newName);

    /// <summary>删除分类（仅当没有流程使用时；分类字段为空即从流程上移出）</summary>
    Task DeleteCategoryAsync(string name);
}

/// <summary>流程分类统计</summary>
public class FlowCategoryDto
{
    public string Name { get; set; } = string.Empty;

    /// <summary>该分类下的流程数</summary>
    public int Count { get; set; }
}

public class SysFlowDefinitionService(IRepository<SysFlowDefinition> repository) : ISysFlowDefinitionService
{
    public async Task<PageResult<FlowDefinitionDto>> GetPageListAsync(FlowDefinitionQueryDto query)
    {
        var keyword = query.Keyword?.Trim();
        var predicate = Expressionable.Create<SysFlowDefinition>()
            .AndIF(!string.IsNullOrWhiteSpace(keyword),
                x => x.FlowCode.Contains(keyword!) || x.FlowName.Contains(keyword!))
            .AndIF(!string.IsNullOrWhiteSpace(query.Category), x => x.Category == query.Category)
            .ToExpression();
        var page = await repository.GetPageListAsync(predicate, query);
        return PageResult<FlowDefinitionDto>.Of(
            page.Items.Select(ToDto).ToList(), page.Total, page.PageIndex, page.PageSize);
    }

    public async Task<FlowDefinitionDto?> GetDetailAsync(long id)
    {
        var definition = await repository.GetByIdAsync(id);
        return definition == null ? null : ToDto(definition);
    }

    public async Task<long> CreateAsync(FlowDefinitionSaveDto dto)
    {
        ValidateNodeJson(dto.NodeJson);

        // 编码：未指定则系统生成数字编号（从 100 起自增）
        var flowCode = string.IsNullOrWhiteSpace(dto.FlowCode)
            ? await GenerateNextCodeAsync()
            : dto.FlowCode.Trim();

        var maxVersion = (await repository.GetListAsync(x => x.FlowCode == flowCode))
            .Select(x => x.Version).DefaultIfEmpty(0).Max();

        var definition = new SysFlowDefinition
        {
            FlowCode = flowCode,
            Category = dto.Category,
            FlowName = dto.FlowName,
            Version = maxVersion + 1,
            NodeJson = dto.NodeJson,
            Status = 0, // 新版本默认停用，确认启用走 Enable
            Remark = dto.Remark
        };
        await repository.InsertAsync(definition);
        return definition.Id;
    }

    public async Task UpdateAsync(long id, FlowDefinitionSaveDto dto)
    {
        ValidateNodeJson(dto.NodeJson);
        var definition = await repository.GetByIdAsync(id)
                         ?? throw new BusinessException("流程定义不存在", ErrorCodes.FLOW_DEF_NOT_FOUND);
        definition.FlowName = dto.FlowName;
        definition.Category = dto.Category;
        definition.NodeJson = dto.NodeJson;
        definition.Remark = dto.Remark;
        await repository.UpdateAsync(definition);
    }

    public async Task DeleteAsync(long id)
    {
        var definition = await repository.GetByIdAsync(id)
                         ?? throw new BusinessException("流程定义不存在", ErrorCodes.FLOW_DEF_NOT_FOUND);
        if (definition.Status == 1)
        {
            throw new BusinessException("启用中的流程不允许删除，请先停用", ErrorCodes.FLOW_DEF_DELETE_ENABLED_FORBIDDEN);
        }
        await repository.DeleteAsync(definition);
    }

    public async Task EnableAsync(long id)
    {
        var definition = await repository.GetByIdAsync(id)
                         ?? throw new BusinessException("流程定义不存在", ErrorCodes.FLOW_DEF_NOT_FOUND);
        // 同编码其他版本全部停用（事务保证任一时刻至多一个启用版本）
        await repository.TransactionAsync(async () =>
        {
            await repository.UpdateWhereAsync(
                x => x.FlowCode == definition.FlowCode && x.Id != id,
                x => new SysFlowDefinition { Status = 0 });
            definition.Status = 1;
            await repository.UpdateAsync(definition);
            return true;
        });
    }

    public Task DisableAsync(long id) =>
        repository.UpdateWhereAsync(x => x.Id == id, x => new SysFlowDefinition { Status = 0 });

    public void ValidateNodeJson(string nodeJson)
    {
        var graph = FlowGraph.Parse(nodeJson)
                    ?? throw new BusinessException("节点配置 JSON 解析失败", ErrorCodes.FLOW_DEF_JSON_INVALID);
        if (graph.Index.Count == 0 || !graph.Index.ContainsKey(graph.Entry))
        {
            throw new BusinessException("节点配置缺少入口节点", ErrorCodes.FLOW_DEF_NO_ENTRY);
        }
        var dup = graph.Nodes.GroupBy(n => n.Code).FirstOrDefault(g => g.Count() > 1);
        if (dup != null)
        {
            throw new BusinessException($"节点编码重复: {dup.Key}", ErrorCodes.FLOW_DEF_NODE_CODE_DUP);
        }
        foreach (var node in graph.Nodes)
        {
            if (node.Type == FlowNodeType.Approval && (node.Approvers == null || node.Approvers.Count == 0))
            {
                throw new BusinessException($"审批节点「{node.Name ?? node.Code}」未配置审批人规则", ErrorCodes.FLOW_DEF_APPROVER_MISSING);
            }
        }
    }

    public async Task<List<FlowCategoryDto>> GetCategoriesAsync()
    {
        var rows = await repository.Db.Ado.SqlQueryAsync<FlowCategoryDto>(
            "SELECT Category AS Name, COUNT(1) AS Count FROM sys_flow_definition " +
            "WHERE Category IS NOT NULL AND Category <> '' AND IsDeleted = 0 GROUP BY Category ORDER BY Category");
        return rows;
    }

    public async Task RenameCategoryAsync(string oldName, string newName)
    {
        oldName = oldName?.Trim() ?? string.Empty;
        newName = newName?.Trim() ?? string.Empty;
        if (oldName.IsNullOrEmpty() || newName.IsNullOrEmpty())
        {
            throw new BusinessException("分类名不能为空", ErrorCodes.FLOW_CATEGORY_NAME_REQUIRED);
        }
        if (oldName == newName)
        {
            return;
        }
        await repository.UpdateWhereAsync(
            x => x.Category == oldName, x => new SysFlowDefinition { Category = newName });
    }

    public async Task DeleteCategoryAsync(string name)
    {
        name = name?.Trim() ?? string.Empty;
        if (name.IsNullOrEmpty())
        {
            return;
        }
        var used = await repository.CountAsync(x => x.Category == name);
        if (used > 0)
        {
            throw new BusinessException($"分类「{name}」下还有 {used} 个流程，请先在流程上移出该分类", ErrorCodes.FLOW_CATEGORY_IN_USE);
        }
    }

    /// <summary>生成下一个数字流程编号（100 起自增；兼容历史非数字编码不参与计数）</summary>
    private async Task<string> GenerateNextCodeAsync()
    {
        var max = await repository.Db.Ado.GetIntAsync(
            "SELECT ISNULL(MAX(TRY_CAST(FlowCode AS INT)), 99) FROM sys_flow_definition WHERE ISNUMERIC(FlowCode) = 1");
        return (max + 1).ToString();
    }

    private static FlowDefinitionDto ToDto(SysFlowDefinition x) => new()
    {
        Id = x.Id,
        FlowCode = x.FlowCode,
        Category = x.Category,
        FlowName = x.FlowName,
        Version = x.Version,
        NodeJson = x.NodeJson,
        Status = x.Status,
        Remark = x.Remark,
        CreateTime = x.CreateTime
    };
}
