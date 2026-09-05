using System.ComponentModel.DataAnnotations;

namespace NetBase.Model.Dtos;

/// <summary>修改自己资料请求（昵称/手机/邮箱；用户名与状态不可自改）</summary>
public class UpdateProfileDto
{
    /// <summary>昵称</summary>
    [StringLength(50, ErrorMessage = "昵称长度不能超过 50")]
    public string? NickName { get; set; }

    /// <summary>手机号</summary>
    [Phone(ErrorMessage = "手机号格式不正确")]
    [StringLength(20)]
    public string? Phone { get; set; }

    /// <summary>邮箱</summary>
    [EmailAddress(ErrorMessage = "邮箱格式不正确")]
    [StringLength(100)]
    public string? Email { get; set; }
}
