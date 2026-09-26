using System.ComponentModel.DataAnnotations;

namespace NetBase.Model.Dtos;

/// <summary>忘记密码：发送验证码请求</summary>
public class ForgotPasswordDto
{
    /// <summary>账号</summary>
    [Required(ErrorMessage = "账号不能为空")]
    [StringLength(50)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>账号预留邮箱</summary>
    [Required(ErrorMessage = "邮箱不能为空")]
    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;
}

/// <summary>忘记密码：自助重置密码请求（区别于管理员 ResetPasswordDto）</summary>
public class SelfResetPasswordDto
{
    /// <summary>账号</summary>
    [Required(ErrorMessage = "账号不能为空")]
    [StringLength(50)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>账号预留邮箱（须与发码时一致）</summary>
    [Required(ErrorMessage = "邮箱不能为空")]
    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;

    /// <summary>邮箱验证码（6 位，5 分钟有效，一次使用）</summary>
    [Required(ErrorMessage = "验证码不能为空")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "验证码须为 6 位")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "验证码须为 6 位数字")]
    public string Code { get; set; } = string.Empty;

    /// <summary>新密码</summary>
    [Required(ErrorMessage = "新密码不能为空")]
    [StringLength(64, MinimumLength = 6, ErrorMessage = "密码长度须为 6-64 位")]
    public string NewPassword { get; set; } = string.Empty;
}
