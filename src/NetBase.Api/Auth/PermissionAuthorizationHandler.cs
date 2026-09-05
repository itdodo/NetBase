using Microsoft.AspNetCore.Authorization;
using NetBase.Service.Sys;

namespace NetBase.Api.Auth;

/// <summary>
/// 接口权限标注：要求当前用户拥有指定权限码（来自菜单按钮配置，如 sys:user:add）。
/// 用法：[HasPermission("sys:user:add")]，需配合 Program 中的权限策略提供器注册。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class HasPermissionAttribute(string permission) : AuthorizeAttribute(PolicyPrefix + permission)
{
    public const string PolicyPrefix = "perm:";

    public string Permission { get; } = permission;
}

/// <summary>权限码要求</summary>
public class HasPermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>
/// 权限码授权处理器：比对当前用户权限集合（进程内缓存）与策略要求的权限码。
/// </summary>
public class PermissionAuthorizationHandler(IPermissionService permissionService)
    : AuthorizationHandler<HasPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HasPermissionRequirement requirement)
    {
        var userIdClaim = context.User.FindFirst("uid")?.Value;
        if (!long.TryParse(userIdClaim, out var userId))
        {
            return;
        }

        var permissions = await permissionService.GetUserPermissionsAsync(userId);
        if (permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}

/// <summary>
/// 动态权限策略提供器：为 "perm:xxx" 形式的策略名实时生成授权策略，
/// 使 [HasPermission] 特性无需预先注册每个权限码。
/// </summary>
public class PermissionPolicyProvider(Microsoft.Extensions.Options.IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(HasPermissionAttribute.PolicyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var permission = policyName[HasPermissionAttribute.PolicyPrefix.Length..];
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new HasPermissionRequirement(permission))
                .Build();
        }

        return await base.GetPolicyAsync(policyName);
    }
}
