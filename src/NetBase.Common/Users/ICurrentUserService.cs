namespace NetBase.Common.Users;

/// <summary>
/// 当前登录用户信息。
/// 当前阶段（认证未接入）恒为空，控制器写入审计字段时回退为 system；
/// 接入 JWT 后由实现类从 ClaimsPrincipal 解析，业务代码无需改动。
/// </summary>
public interface ICurrentUserService
{
    /// <summary>当前用户ID，未认证为 null</summary>
    long? UserId { get; }

    /// <summary>当前用户名，未认证为 null</summary>
    string? UserName { get; }
}
