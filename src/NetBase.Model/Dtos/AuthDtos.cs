using System.ComponentModel.DataAnnotations;

namespace NetBase.Model.Dtos;

/// <summary>登录请求</summary>
public class LoginRequestDto
{
    /// <summary>用户名</summary>
    [Required(ErrorMessage = "用户名不能为空")]
    [StringLength(50, ErrorMessage = "用户名长度不能超过 50")]
    public string UserName { get; set; } = string.Empty;

    /// <summary>密码</summary>
    [Required(ErrorMessage = "密码不能为空")]
    [StringLength(64, MinimumLength = 6, ErrorMessage = "密码长度须为 6-64 位")]
    public string Password { get; set; } = string.Empty;

    /// <summary>验证码ID（验证码启用时必填）</summary>
    public string? CaptchaId { get; set; }

    /// <summary>验证码</summary>
    [StringLength(10)]
    public string? CaptchaCode { get; set; }
}

/// <summary>刷新令牌请求</summary>
public class RefreshRequestDto
{
    /// <summary>刷新令牌</summary>
    [Required(ErrorMessage = "刷新令牌不能为空")]
    [StringLength(128)]
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>修改自己密码请求（需验证旧密码）</summary>
public class ChangePasswordDto
{
    /// <summary>旧密码</summary>
    [Required(ErrorMessage = "旧密码不能为空")]
    [StringLength(64)]
    public string OldPassword { get; set; } = string.Empty;

    /// <summary>新密码（复杂度由服务端策略校验）</summary>
    [Required(ErrorMessage = "新密码不能为空")]
    [StringLength(64, MinimumLength = 6, ErrorMessage = "密码长度须为 6-64 位")]
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>批量强制下线请求（会话 ID 列表）</summary>
public class BatchKickDto
{
    /// <summary>会话 ID 列表（一次最多 100 个）</summary>
    [Required(ErrorMessage = "请选择要下线的会话")]
    [MinLength(1, ErrorMessage = "请选择要下线的会话")]
    [MaxLength(100, ErrorMessage = "一次最多下线 100 个会话")]
    public List<long> Ids { get; set; } = [];
}
