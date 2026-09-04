using NetBase.Common.Results;

namespace NetBase.Model.Dtos;

/// <summary>角色分页查询条件</summary>
public class RoleQueryDto : PageQuery
{
    /// <summary>角色名称/编码关键字</summary>
    public string? Keyword { get; set; }

    /// <summary>状态过滤</summary>
    public int? Status { get; set; }
}

/// <summary>角色返回</summary>
public class RoleDto
{
    public long Id { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public string RoleCode { get; set; } = string.Empty;

    /// <summary>状态：0-停用 1-启用</summary>
    public int Status { get; set; }

    public int Sort { get; set; }

    public DateTime CreateTime { get; set; }
}

/// <summary>角色简要信息（用于下拉框、用户角色展示）</summary>
public class RoleSimpleDto
{
    public long Id { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public string RoleCode { get; set; } = string.Empty;
}

/// <summary>创建/更新角色请求</summary>
public class RoleSaveDto
{
    /// <summary>角色名称</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>角色编码（唯一）</summary>
    public string RoleCode { get; set; } = string.Empty;

    /// <summary>状态：0-停用 1-启用</summary>
    public int Status { get; set; } = 1;

    /// <summary>排序号</summary>
    public int Sort { get; set; }
}

/// <summary>分配菜单请求</summary>
public class AssignMenusDto
{
    /// <summary>菜单ID列表</summary>
    public List<long> MenuIds { get; set; } = [];
}
