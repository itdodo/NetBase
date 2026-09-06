namespace NetBase.Model.Enums;

/// <summary>通用状态</summary>
public enum StatusEnum
{
    /// <summary>停用</summary>
    Disabled = 0,

    /// <summary>启用</summary>
    Enabled = 1
}

/// <summary>菜单类型</summary>
public enum MenuTypeEnum
{
    /// <summary>目录</summary>
    Directory = 1,

    /// <summary>菜单</summary>
    Menu = 2,

    /// <summary>按钮（权限点）</summary>
    Button = 3
}

/// <summary>数据范围（角色维度）</summary>
public enum DataScopeEnum
{
    /// <summary>全部数据</summary>
    All = 1,

    /// <summary>自定义部门（SysRoleDept 勾选）</summary>
    Custom = 2,

    /// <summary>本部门</summary>
    Dept = 3,

    /// <summary>本部门及以下</summary>
    DeptAndChild = 4,

    /// <summary>仅本人</summary>
    Self = 5
}
