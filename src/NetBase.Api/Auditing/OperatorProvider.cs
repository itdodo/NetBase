using Microsoft.AspNetCore.Http;
using NetBase.Common.Users;
using NetBase.Repository.Auditing;

namespace NetBase.Api.Auditing;

/// <summary>操作人提供者：从当前登录态解析（JWT 接入后自动生效）</summary>
public class OperatorProvider(IHttpContextAccessor httpContextAccessor) : IOperatorProvider
{
    public string? OperatorName => httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
}
