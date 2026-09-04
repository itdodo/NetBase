using NetBase.Common.Results;

namespace NetBase.Model.Dtos;

/// <summary>用户分页查询条件</summary>
public class UserQueryDto : PageQuery
{
    /// <summary>用户名/昵称关键字</summary>
    public string? Keyword { get; set; }

    /// <summary>状态过滤</summary>
    public int? Status { get; set; }
}

/// <summary>用户列表/详情返回</summary>
public class UserDto
{
    public long Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string? NickName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    /// <summary>状态：0-停用 1-启用</summary>
    public int Status { get; set; }

    public DateTime? LastLoginTime { get; set; }

    public DateTime CreateTime { get; set; }

    /// <summary>拥有的角色</summary>
    public List<RoleSimpleDto> Roles { get; set; } = [];
}

/// <summary>创建用户请求</summary>
public class UserCreateDto
{
    /// <summary>用户名（登录账号）</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>昵称</summary>
    public string? NickName { get; set; }

    /// <summary>手机号</summary>
    public string? Phone { get; set; }

    /// <summary>邮箱</summary>
    public string? Email { get; set; }

    /// <summary>初始密码，为空则使用默认密码 123456</summary>
    public string? Password { get; set; }

    /// <summary>状态：0-停用 1-启用</summary>
    public int Status { get; set; } = 1;

    /// <summary>角色ID列表</summary>
    public List<long> RoleIds { get; set; } = [];
}

/// <summary>更新用户请求</summary>
public class UserUpdateDto
{
    public string? NickName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    /// <summary>状态：0-停用 1-启用</summary>
    public int Status { get; set; } = 1;

    /// <summary>角色ID列表（传入则全量重设）</summary>
    public List<long>? RoleIds { get; set; }
}

/// <summary>重置密码请求</summary>
public class ResetPasswordDto
{
    /// <summary>新密码，为空则使用默认密码 123456</summary>
    public string? NewPassword { get; set; }
}

/// <summary>分配角色请求</summary>
public class AssignRolesDto
{
    /// <summary>角色ID列表</summary>
    public List<long> RoleIds { get; set; } = [];
}
