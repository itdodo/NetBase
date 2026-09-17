using System.ComponentModel.DataAnnotations;
using NetBase.Common.Validation;
using MiniExcelLibs.Attributes;
using NetBase.Common.Results;

using System.Text.Json.Serialization;
namespace NetBase.Model.Dtos;

/// <summary>用户分页查询条件</summary>
public class UserQueryDto : PageQuery
{
    /// <summary>用户名/昵称关键字</summary>
    [StringLength(50, ErrorMessage = "关键字长度不能超过 50")]
    public string? Keyword { get; set; }

    /// <summary>状态过滤</summary>
    [Range(0, 1, ErrorMessage = "状态取值无效")]
    public int? Status { get; set; }

    /// <summary>部门过滤（含下级部门）</summary>
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    public long? DeptId { get; set; }
}

/// <summary>用户列表/详情返回</summary>
public class UserDto
{
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    public long Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string? NickName { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    /// <summary>状态：0-停用 1-启用</summary>
    public int Status { get; set; }

    /// <summary>头像地址</summary>
    public string? Avatar { get; set; }

    public DateTime? LastLoginTime { get; set; }

    public DateTime CreateTime { get; set; }

    /// <summary>拥有的角色</summary>
    public List<RoleSimpleDto> Roles { get; set; } = [];

    /// <summary>所属部门ID</summary>
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    public long DeptId { get; set; }

    /// <summary>所属部门名称</summary>
    public string? DeptName { get; set; }

    /// <summary>并发版本（编辑保存时原样回传，服务端比对拦截陈旧提交）</summary>
    public long Version { get; set; }
}

/// <summary>创建用户请求</summary>
public class UserCreateDto
{
    /// <summary>用户名（登录账号）</summary>
    [Required(ErrorMessage = "用户名不能为空")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "用户名长度须为 2-50 个字符")]
    [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "用户名只能包含字母、数字、下划线")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>昵称</summary>
    [StringLength(50, ErrorMessage = "昵称长度不能超过 50")]
    public string? NickName { get; set; }

    /// <summary>手机号</summary>
    [OptionalPhone]
    [StringLength(20, ErrorMessage = "手机号长度不能超过 20")]
    public string? Phone { get; set; }

    /// <summary>邮箱</summary>
    [OptionalEmail]
    [StringLength(100, ErrorMessage = "邮箱长度不能超过 100")]
    public string? Email { get; set; }

    /// <summary>初始密码，为空则使用默认密码（复杂度由服务端策略校验）</summary>
    [StringLength(64, MinimumLength = 6, ErrorMessage = "密码长度须为 6-64 位")]
    public string? Password { get; set; }

    /// <summary>状态：0-停用 1-启用</summary>
    [Range(0, 1, ErrorMessage = "状态取值无效")]
    public int Status { get; set; } = 1;

    /// <summary>角色ID列表</summary>
    public List<long> RoleIds { get; set; } = [];

    /// <summary>所属部门ID（可选，留空 = 未分配部门）</summary>
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    public long? DeptId { get; set; }
}

/// <summary>更新用户请求</summary>
public class UserUpdateDto
{
    /// <summary>昵称</summary>
    [StringLength(50, ErrorMessage = "昵称长度不能超过 50")]
    public string? NickName { get; set; }

    /// <summary>手机号</summary>
    [OptionalPhone]
    [StringLength(20, ErrorMessage = "手机号长度不能超过 20")]
    public string? Phone { get; set; }

    /// <summary>邮箱</summary>
    [OptionalEmail]
    [StringLength(100, ErrorMessage = "邮箱长度不能超过 100")]
    public string? Email { get; set; }

    /// <summary>状态：0-停用 1-启用</summary>
    [Range(0, 1, ErrorMessage = "状态取值无效")]
    public int Status { get; set; } = 1;

    /// <summary>角色ID列表（传入则全量重设）</summary>
    public List<long>? RoleIds { get; set; }

    /// <summary>所属部门ID（可选，留空 = 未分配部门）</summary>
    [JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]
    public long? DeptId { get; set; }

    /// <summary>并发版本（编辑时读取、保存时回传；与他人先提交的版本不一致则拒绝，不传则跳过校验）</summary>
    public long? Version { get; set; }
}

/// <summary>重置密码请求</summary>
public class ResetPasswordDto
{
    /// <summary>新密码，为空则使用默认密码（复杂度由服务端策略校验）</summary>
    [StringLength(64, MinimumLength = 6, ErrorMessage = "密码长度须为 6-64 位")]
    public string? NewPassword { get; set; }
}

/// <summary>分配角色请求</summary>
public class AssignRolesDto
{
    /// <summary>角色ID列表</summary>
    public List<long> RoleIds { get; set; } = [];
}


/// <summary>用户导入行（与导入模板中文列头对应）</summary>
public class UserImportRow
{
    /// <summary>用户名</summary>
    [ExcelColumnName("用户名")]
    public string? UserName { get; set; }

    /// <summary>昵称</summary>
    [ExcelColumnName("昵称")]
    public string? NickName { get; set; }

    /// <summary>手机号</summary>
    [ExcelColumnName("手机号")]
    public string? Phone { get; set; }

    /// <summary>邮箱</summary>
    [ExcelColumnName("邮箱")]
    public string? Email { get; set; }

    /// <summary>初始密码（留空用系统默认）</summary>
    [ExcelColumnName("初始密码")]
    public string? Password { get; set; }

    /// <summary>角色编码（多个用逗号分隔，可选）</summary>
    [ExcelColumnName("角色编码")]
    public string? RoleCodes { get; set; }
}

/// <summary>用户导入结果</summary>
public class UserImportResultDto
{
    public int SuccessCount { get; set; }

    public List<string> Errors { get; set; } = [];
}
