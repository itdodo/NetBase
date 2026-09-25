using System.Text.Json.Serialization;

namespace NetBase.Model.Dtos;

/// <summary>当前登录用户信息 + 权限码（前端刷新后恢复会话用）</summary>
public class ProfileDto
{
    /// <summary>用户信息（未认证为 null）</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UserDto? User { get; set; }

    /// <summary>权限码集合（v-permission 与动态路由判定依据）</summary>
    public List<string> Permissions { get; set; } = [];
}
