# -*- coding: utf-8 -*-
"""批量为 ID 语义 long 属性加 LongToStringConverter（实体 + DTO 全覆盖）"""
import re, os

os.chdir(r'E:\02AI\ZCode\Net通用基础框架\src')

USING = 'using System.Text.Json.Serialization;'
CONV = '[JsonConverter(typeof(NetBase.Common.Json.LongToStringConverter))]'

# ID 语义属性名（雪花引用或主键）
id_props = r'(?:Id|DeptId|ParentId|UserId|RoleId|MenuId|ReceiverId|DictTypeId|UploadUserId|OwnerUserId)'

files = [
    # 实体
    'NetBase.Model/Entities/BaseEntity.cs',
    'NetBase.Model/Entities/SysUser.cs',
    'NetBase.Model/Entities/SysDept.cs',
    'NetBase.Model/Entities/SysMenu.cs',
    'NetBase.Model/Entities/SysUserRole.cs',
    'NetBase.Model/Entities/SysRoleMenu.cs',
    'NetBase.Model/Entities/SysRoleDept.cs',
    'NetBase.Model/Entities/SysMessage.cs',
    'NetBase.Model/Entities/SysFile.cs',
    'NetBase.Model/Entities/SysLoginLog.cs',
    'NetBase.Model/Entities/SysOperationLog.cs',
    # DTO
    'NetBase.Model/Dtos/UserDtos.cs',
    'NetBase.Model/Dtos/RoleDtos.cs',
    'NetBase.Model/Dtos/MenuDtos.cs',
    'NetBase.Model/Dtos/DeptDtos.cs',
    'NetBase.Model/Dtos/DictDtos.cs',
    'NetBase.Model/Dtos/NoticeDtos.cs',
    'NetBase.Model/Dtos/ConfigDtos.cs',
    'NetBase.Model/Dtos/AuthDtos.cs',
    'NetBase.Model/Dtos/ProfileDtos.cs',
    'NetBase.Service/Sys/ISysAuthService.cs',   # SessionDto / LoginResult
    'NetBase.Service/Sys/ISysLogService.cs',    # Log DTO
    'NetBase.Service/Sys/ISysMessageService.cs',# MessageSendDto
    'NetBase.Service/Sys/ISysFileService.cs',   # FileUploadResult.Id
    'NetBase.Service/Sys/IDashboardService.cs', # DeptUserCount 无 id，安全
]

total = 0
for f in files:
    if not os.path.exists(f):
        print('missing', f)
        continue
    c = open(f, encoding='utf-8').read()
    orig = c
    # 已带 JsonConverter 或 IsIgnore 的属性行跳过（匹配 SugarColumn/其它特性行前面的位置）
    pattern = re.compile(
        r'^(?P<indent>    )(?!.*JsonConverter)(?P<attrs>(?:\[(?!.*JsonConverter)[^\n]*\]\n)*)'
        r'(?P<decl>    public long ' + id_props + r' \{ get)',
        re.M)

    def repl(m):
        indent = m.group('indent')
        attrs = m.group('attrs')
        decl = m.group('decl')
        # 若已有特性块，把 converter 特性插到最前；否则在声明前插入完整特性行
        if attrs:
            return f'{indent}{CONV}\n{indent}{attrs}{decl}'
        return f'{indent}{CONV}\n{indent}{decl}'

    c2 = pattern.sub(repl, c)
    if c2 != orig:
        count += (len(re.findall(CONV, c2)) - len(re.findall(CONV, orig)))
        c = c2
    # 补 using（若文件有特性标注但缺 using）
    if CONV.split('(')[0] in c and USING not in c and 'NetBase.Common.Json.LongToStringConverter' in c:
        # using 全称引用了 converter，无需 using；但 JsonConverterAttribute 需要using
        pass
    # System.Text.Json.Serialization 的 using：JsonConverterAttribute 在该命名空间
    if '[JsonConverter(typeof(' in c and USING not in c:
        lines = c.split('\n')
        for i, line in enumerate(lines):
            if line.startswith('namespace '):
                lines.insert(i, USING)
                break
        c = '\n'.join(lines)
    open(f, 'w', encoding='utf-8').write(c)

print('total conversions added:', count)
