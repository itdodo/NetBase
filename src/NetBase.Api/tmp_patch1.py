# -*- coding: utf-8 -*-
# 1) 统一 IP 获取：AuthController/OperationLogFilter 改用 GetClientIp 扩展
p = 'src/NetBase.Api/Controllers/Auth/AuthController.cs'
c = open(p, encoding='utf-8').read()
c = c.replace('using NetBase.Model.Dtos;', 'using NetBase.Api.Extensions;\nusing NetBase.Model.Dtos;')
c = c.replace('HttpContext.Connection.RemoteIpAddress?.ToString(),', 'HttpContext.GetClientIp(),')
open(p, 'w', encoding='utf-8').write(c)
print('auth ip ok')

# 2) OperationLogFilter 用统一扩展 + TruncateTo
p = 'src/NetBase.Api/Filters/OperationLogFilter.cs'
c = open(p, encoding='utf-8').read()
c = c.replace('using NetBase.Common.Security;', 'using NetBase.Common.Extensions;\nusing NetBase.Common.Security;')
c = c.replace('''            Path = Truncate(context.HttpContext.Request.Path.Value, 200),''',
'''            Path = context.HttpContext.Request.Path.Value.TruncateTo(200),''')
c = c.replace('''            ErrorMessage = Truncate(executed.Exception?.Message, 500),''',
'''            ErrorMessage = executed.Exception?.Message.TruncateTo(500),''')
c = c.replace('''            Ip = GetClientIp(context.HttpContext)''', '''            Ip = context.HttpContext.GetClientIp()''')
# 删除私有 GetClientIp/Truncate（已由扩展替代）
c = c.replace('''
    private static string? GetClientIp(HttpContext httpContext)
    {
        // 反向代理场景优先取转发头第一段
        var forwarded = httpContext.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrEmpty(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }
        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];''', '')
open(p, 'w', encoding='utf-8').write(c)
print('filter ok')

# 3) SysAuthService：TruncateTo + 登录日志 UA 截断统一
p = 'src/NetBase.Service/Sys/SysAuthService.cs'
c = open(p, encoding='utf-8').read()
c = c.replace('using NetBase.Common.Security;', 'using NetBase.Common.Extensions;\nusing NetBase.Common.Security;')
c = c.replace('UserAgent = userAgent?.Length > 255 ? userAgent[..255] : userAgent', 'UserAgent = userAgent.TruncateTo(255)')
c = c.replace('''    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max];

''', '')
open(p, 'w', encoding='utf-8').write(c)
print('auth service ok')

# 4) 登录日志记录补 XFF（AuthController 传 GetClientIp 已覆盖 ✓）

# 5) 消息接收人校验
p = 'src/NetBase.Service/Sys/SysMessageService.cs'
c = open(p, encoding='utf-8').read()
c = c.replace('''public class SysMessageService(IRepository<SysMessage> repository) : ISysMessageService
{
    public async Task<long> SendAsync(MessageSendDto dto, string senderName)
    {''',
'''public class SysMessageService(IRepository<SysMessage> repository, IRepository<SysUser> userRepository) : ISysMessageService
{
    public async Task<long> SendAsync(MessageSendDto dto, string senderName)
    {
        // 接收人必须存在（防消息发往无效账号）
        if (!await userRepository.AnyAsync(x => x.Id == dto.ReceiverId))
        {
            throw new BusinessException($"接收用户不存在（Id={dto.ReceiverId}）", ApiResultCode.BadRequest);
        }
''')
open(p, 'w', encoding='utf-8').write(c)
print('message ok')
