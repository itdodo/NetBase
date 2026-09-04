using Microsoft.Extensions.Logging;
using NetBase.Common.Security;
using NetBase.Model.Entities;
using NetBase.Model.Enums;
using SqlSugar;

namespace NetBase.Repository.DbContexts;

/// <summary>
/// CodeFirst 建表 + 种子数据（admin 账号、管理员角色、初始菜单）。
/// 幂等：仅在对应表为空时写入。
/// </summary>
public class DbSeeder
{
    private readonly SqlSugarContext _context;
    private readonly ILogger<DbSeeder>? _logger;

    public DbSeeder(SqlSugarContext context, ILogger<DbSeeder>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    public void Run()
    {
        _context.InitDatabase();
        _logger?.LogInformation("CodeFirst 建表完成");

        var db = _context.Client;
        var now = DateTime.Now;

        long adminRoleId;
        if (db.Queryable<SysRole>().Any(x => x.RoleCode == "admin"))
        {
            adminRoleId = db.Queryable<SysRole>().First(x => x.RoleCode == "admin").Id;
        }
        else
        {
            var role = new SysRole
            {
                RoleName = "超级管理员",
                RoleCode = "admin",
                Status = (int)StatusEnum.Enabled,
                Sort = 0,
                CreateTime = now,
                CreateBy = "system"
            };
            db.Insertable(role).ExecuteCommand();
            adminRoleId = role.Id;
            _logger?.LogInformation("种子数据：默认角色 admin 已写入");
        }

        if (!db.Queryable<SysUser>().Any(x => x.UserName == "admin"))
        {
            var admin = new SysUser
            {
                UserName = "admin",
                Password = PasswordHelper.Encrypt(PasswordHelper.DefaultPassword),
                NickName = "系统管理员",
                Status = (int)StatusEnum.Enabled,
                CreateTime = now,
                CreateBy = "system"
            };
            db.Insertable(admin).ExecuteCommand();
            db.Insertable(new SysUserRole { UserId = admin.Id, RoleId = adminRoleId, CreateTime = now }).ExecuteCommand();
            _logger?.LogInformation("种子数据：admin 账号已创建，默认密码 {Password}", PasswordHelper.DefaultPassword);
        }

        if (!db.Queryable<SysMenu>().Any())
        {
            SeedMenus(db, adminRoleId, now);
            _logger?.LogInformation("种子数据：初始菜单已写入");
        }
    }

    private void SeedMenus(ISqlSugarClient db, long adminRoleId, DateTime now)
    {
        // 逐层插入：目录 -> 菜单 -> 按钮，子级依赖父级自增ID
        var systemDir = db.Insertable(new SysMenu
        {
            ParentId = 0,
            MenuName = "系统管理",
            MenuType = (int)MenuTypeEnum.Directory,
            Path = "/system",
            Icon = "setting",
            Sort = 1,
            CreateTime = now,
            CreateBy = "system"
        }).ExecuteReturnEntity();

        var monitorDir = db.Insertable(new SysMenu
        {
            ParentId = 0,
            MenuName = "系统监控",
            MenuType = (int)MenuTypeEnum.Directory,
            Path = "/monitor",
            Icon = "monitor",
            Sort = 2,
            CreateTime = now,
            CreateBy = "system"
        }).ExecuteReturnEntity();

        var userMenu = db.Insertable(new SysMenu
        {
            ParentId = systemDir.Id,
            MenuName = "用户管理",
            MenuType = (int)MenuTypeEnum.Menu,
            Path = "/system/user",
            Component = "system/user/index",
            Permission = "sys:user:list",
            Sort = 1,
            CreateTime = now,
            CreateBy = "system"
        }).ExecuteReturnEntity();

        var roleMenu = db.Insertable(new SysMenu
        {
            ParentId = systemDir.Id,
            MenuName = "角色管理",
            MenuType = (int)MenuTypeEnum.Menu,
            Path = "/system/role",
            Component = "system/role/index",
            Permission = "sys:role:list",
            Sort = 2,
            CreateTime = now,
            CreateBy = "system"
        }).ExecuteReturnEntity();

        db.Insertable(new SysMenu
        {
            ParentId = systemDir.Id,
            MenuName = "菜单管理",
            MenuType = (int)MenuTypeEnum.Menu,
            Path = "/system/menu",
            Component = "system/menu/index",
            Permission = "sys:menu:list",
            Sort = 3,
            CreateTime = now,
            CreateBy = "system"
        }).ExecuteCommand();

        db.Insertable(new SysMenu
        {
            ParentId = monitorDir.Id,
            MenuName = "在线用户",
            MenuType = (int)MenuTypeEnum.Menu,
            Path = "/monitor/online",
            Component = "monitor/online/index",
            Permission = "monitor:online:list",
            Sort = 1,
            CreateTime = now,
            CreateBy = "system"
        }).ExecuteCommand();

        var buttons = new List<SysMenu>();
        buttons.AddRange(BuildCrudButtons(userMenu.Id, "sys:user", now));
        buttons.AddRange(BuildCrudButtons(roleMenu.Id, "sys:role", now));
        buttons.AddRange(BuildCrudButtons(systemDir.Id, "sys:menu", now));
        db.Insertable(buttons).ExecuteCommand();

        // 全部菜单授予 admin 角色
        var menuIds = db.Queryable<SysMenu>().Select(x => x.Id).ToList();
        db.Insertable(menuIds.Select(menuId => new SysRoleMenu
        {
            RoleId = adminRoleId,
            MenuId = menuId,
            CreateTime = now
        }).ToList()).ExecuteCommand();
    }

    private static List<SysMenu> BuildCrudButtons(long parentId, string permissionPrefix, DateTime now) =>
    [
        new()
        {
            ParentId = parentId, MenuName = "新增", MenuType = (int)MenuTypeEnum.Button,
            Permission = $"{permissionPrefix}:add", Sort = 1, CreateTime = now, CreateBy = "system"
        },
        new()
        {
            ParentId = parentId, MenuName = "编辑", MenuType = (int)MenuTypeEnum.Button,
            Permission = $"{permissionPrefix}:edit", Sort = 2, CreateTime = now, CreateBy = "system"
        },
        new()
        {
            ParentId = parentId, MenuName = "删除", MenuType = (int)MenuTypeEnum.Button,
            Permission = $"{permissionPrefix}:delete", Sort = 3, CreateTime = now, CreateBy = "system"
        }
    ];
}
