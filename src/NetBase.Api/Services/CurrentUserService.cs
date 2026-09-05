using System.Security.Claims;
using NetBase.Common.Users;

namespace NetBase.Api.Services;

/// <summary>
/// 从 HttpContext.User 解析当前用户；认证接入后此实现自动生效，无需改动业务代码。
/// </summary>
public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private const string UserIdClaim = "uid";

    public long? UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(UserIdClaim);
            return long.TryParse(value, out var id) ? id : null;
        }
    }

    public string? UserName => httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name);
}
