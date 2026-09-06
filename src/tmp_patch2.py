# -*- coding: utf-8 -*-
# AndIF 简化谓词 + 数据范围缓存
import re

# 1) SysUserService BuildPredicateAsync：hasCondition → AndIF
p = 'src/NetBase.Service/Sys/SysUserService.cs'
c = open(p, encoding='utf-8').read()
c = c.replace('''    private async Task<Expression<Func<SysUser, bool>>> BuildPredicateAsync(UserQueryDto query)
    {
        var hasCondition = false;
        var exp = Expressionable.Create<SysUser>();
        if (query.Keyword.IsNotNullOrEmpty())
        {
            hasCondition = true;
            var keyword = query.Keyword!.Trim();
            exp.And(x => x.UserName.Contains(keyword) || (x.NickName != null && x.NickName.Contains(keyword)));
        }
        if (query.Status.HasValue)
        {
            hasCondition = true;
            var status = query.Status.Value;
            exp.And(x => x.Status == status);
        }
        if (query.DeptId.HasValue)
        {
            hasCondition = true;
            var deptIds = await _deptService.GetDeptAndChildIdsAsync(query.DeptId.Value);
            exp.And(x => deptIds.Contains(x.DeptId));
        }
        return exp.ToExpression();
    }''',
'''    private async Task<Expression<Func<SysUser, bool>>> BuildPredicateAsync(UserQueryDto query)
    {
        var keyword = query.Keyword?.Trim();
        var deptIds = query.DeptId.HasValue ? await _deptService.GetDeptAndChildIdsAsync(query.DeptId.Value) : null;
        return Expressionable.Create<SysUser>()
            .AndIF(keyword.IsNotNullOrEmpty(), x => x.UserName.Contains(keyword!) || (x.NickName != null && x.NickName.Contains(keyword!)))
            .AndIF(query.Status.HasValue, x => x.Status == query.Status!.Value)
            .AndIF(deptIds != null, x => deptIds!.Contains(x.DeptId))
            .ToExpression();
    }''')
open(p, 'w', encoding='utf-8').write(c)
print('user ok')

# 2) SysRoleService
p = 'src/NetBase.Service/Sys/SysRoleService.cs'
c = open(p, encoding='utf-8').read()
c = c.replace('''    private Expression<Func<SysRole, bool>>? BuildPredicate(RoleQueryDto query)
    {
        var hasCondition = false;
        var exp = Expressionable.Create<SysRole>();
        if (query.Keyword.IsNotNullOrEmpty())
        {
            hasCondition = true;
            var keyword = query.Keyword!.Trim();
            exp.And(x => x.RoleName.Contains(keyword) || x.RoleCode.Contains(keyword));
        }
        if (query.Status.HasValue)
        {
            hasCondition = true;
            var status = query.Status.Value;
            exp.And(x => x.Status == status);
        }
        return hasCondition ? exp.ToExpression() : null;
    }''',
'''    private Expression<Func<SysRole, bool>>? BuildPredicate(RoleQueryDto query)
    {
        var keyword = query.Keyword?.Trim();
        return Expressionable.Create<SysRole>()
            .AndIF(keyword.IsNotNullOrEmpty(), x => x.RoleName.Contains(keyword!) || x.RoleCode.Contains(keyword!))
            .AndIF(query.Status.HasValue, x => x.Status == query.Status!.Value)
            .ToExpression();
    }''')
open(p, 'w', encoding='utf-8').write(c)
print('role ok')

# 3) SysLogService 两个谓词
p = 'src/NetBase.Service/Sys/SysLogService.cs'
c = open(p, encoding='utf-8').read()
c = c.replace('''    public async Task<PageResult<OperationLogDto>> GetOperationLogPageAsync(LogQueryDto query)
    {
        var hasCondition = false;
        var exp = Expressionable.Create<SysOperationLog>();
        if (query.Keyword.IsNotNullOrEmpty())
        {
            hasCondition = true;
            var keyword = query.Keyword!.Trim();
            exp.And(x => (x.UserName != null && x.UserName.Contains(keyword))
                      || (x.Module != null && x.Module.Contains(keyword))
                      || (x.Action != null && x.Action.Contains(keyword)));
        }
        if (query.Success.HasValue)
        {
            hasCondition = true;
            var success = query.Success.Value == 1;
            exp.And(x => x.Success == success);
        }
        if (query.BeginTime.HasValue)
        {
            hasCondition = true;
            var begin = query.BeginTime.Value;
            exp.And(x => x.CreateTime >= begin);
        }
        if (query.EndTime.HasValue)
        {
            hasCondition = true;
            var end = query.EndTime.Value;
            exp.And(x => x.CreateTime <= end);
        }
        return hasCondition ? exp.ToExpression() : null;
    }''',
'''    public async Task<PageResult<OperationLogDto>> GetOperationLogPageAsync(LogQueryDto query)
    {
        var keyword = query.Keyword?.Trim();
        var success = query.Success.HasValue ? query.Success.Value == 1 : (bool?)null;
        return await operationLogRepository.GetPageListAsync(
            Expressionable.Create<SysOperationLog>()
                .AndIF(keyword.IsNotNullOrEmpty(), x => (x.UserName != null && x.UserName.Contains(keyword!))
                                                       || (x.Module != null && x.Module.Contains(keyword!))
                                                       || (x.Action != null && x.Action.Contains(keyword!)))
                .AndIF(success.HasValue, x => x.Success == success!.Value)
                .AndIF(query.BeginTime.HasValue, x => x.CreateTime >= query.BeginTime!.Value)
                .AndIF(query.EndTime.HasValue, x => x.CreateTime <= query.EndTime!.Value)
                .ToExpression(), query);
    }''')
c = c.replace('''    public async Task<PageResult<LoginLogDto>> GetLoginLogPageAsync(LogQueryDto query)
    {
        var hasCondition = false;
        var exp = Expressionable.Create<SysLoginLog>();
        if (query.Keyword.IsNotNullOrEmpty())
        {
            hasCondition = true;
            var keyword = query.Keyword!.Trim();
            exp.And(x => x.UserName.Contains(keyword)
                      || (x.Message != null && x.Message.Contains(keyword))
                      || (x.Ip != null && x.Ip.Contains(keyword)));
        }
        if (query.Success.HasValue)
        {
            hasCondition = true;
            var success = query.Success.Value == 1;
            exp.And(x => x.Success == success);
        }
        if (query.EndTime.HasValue)
        {
            hasCondition = true;
            var end = query.EndTime.Value;
            exp.And(x => x.CreateTime <= end);
        }
        return hasCondition ? exp.ToExpression() : null;
    }''',
'''    public async Task<PageResult<LoginLogDto>> GetLoginLogPageAsync(LogQueryDto query)
    {
        var keyword = query.Keyword?.Trim();
        var success = query.Success.HasValue ? query.Success.Value == 1 : (bool?)null;
        return await loginLogRepository.GetPageListAsync(
            Expressionable.Create<SysLoginLog>()
                .AndIF(keyword.IsNotNullOrEmpty(), x => x.UserName.Contains(keyword!)
                                                       || (x.Message != null && x.Message.Contains(keyword!))
                                                       || (x.Ip != null && x.Ip.Contains(keyword!)))
                .AndIF(success.HasValue, x => x.Success == success!.Value)
                .AndIF(query.BeginTime.HasValue, x => x.CreateTime >= query.BeginTime!.Value)
                .AndIF(query.EndTime.HasValue, x => x.CreateTime <= query.EndTime!.Value)
                .ToExpression(), query);
    }''')
open(p, 'w', encoding='utf-8').write(c)
print('log ok')

# 4) SysDictService GetPageAsync 内联谓词
p = 'src/NetBase.Service/Sys/SysDictService.cs'
c = open(p, encoding='utf-8').read()
c = c.replace('''    public async Task<PageResult<SysDictType>> GetTypePageAsync(DictTypeQueryDto query)
    {
        var hasCondition = false;
        var exp = Expressionable.Create<SysDictType>();
        if (query.Keyword.IsNotNullOrEmpty())
        {
            hasCondition = true;
            var keyword = query.Keyword!.Trim();
            exp.And(x => x.DictCode.Contains(keyword) || 
