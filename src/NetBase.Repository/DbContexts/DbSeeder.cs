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

        // 版本化迁移：结构性/数据性变更走 db/migrations 顺序脚本（幂等、已应用不重复执行）
        new DbMigrationRunner(_context, _logger).Run();
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
            // ExecuteReturnEntity 回填自增主键；ExecuteCommand 不会回填，后续关联会挂到 Id=0
            db.Insertable(role).ExecuteReturnEntity();
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
            db.Insertable(admin).ExecuteReturnEntity();
            db.Insertable(new SysUserRole { Id = NewId(), UserId = admin.Id, RoleId = adminRoleId, CreateTime = now }).ExecuteCommand();
            _logger?.LogInformation("种子数据：admin 账号已创建，默认密码 {Password}", PasswordHelper.DefaultPassword);
        }

        if (!db.Queryable<SysMenu>().Any())
        {
            SeedMenus(db, adminRoleId, now);
            _logger?.LogInformation("种子数据：初始菜单已写入");
        }

        // 增量种子：为升级库补充新版本菜单（按权限码幂等判断），并授予 admin 角色
        EnsureLogMenus(db, adminRoleId, now);
        EnsureSystemMenus(db, adminRoleId, now);
        EnsureNoticeMenu(db, adminRoleId, now);
        SeedDepts(db, adminRoleId, now);
        // 增量菜单的 CRUD 按钮权限码补齐（否则 admin 写操作 403）
        EnsureCrudButtons(db, "sys:dict:list", adminRoleId, now);
        EnsureCrudButtons(db, "sys:config:list", adminRoleId, now);
        EnsureCrudButtons(db, "sys:notice:list", adminRoleId, now);
        // 系统监控目录下的"服务监控"页（进程指标/缓存诊断）
        EnsureMonitorMenu(db, adminRoleId, now);
        // 系统监控目录下的"定时任务"页（Hangfire 作业管理）
        EnsureJobMenu(db, adminRoleId, now);
        // 系统管理目录下的"流程管理"页（审批流定义，含 CRUD 按钮权限码）
        EnsureFlowMenu(db, adminRoleId, now);
        EnsureCrudButtons(db, "sys:flow:list", adminRoleId, now);
        // 个人办公目录 + 我的待办/已办（个人页面，无权限码，登录可见）
        EnsurePersonalFlowMenus(db, adminRoleId, now);
        // 业务样板菜单（业务办公：报销/采购申请，接入审批流的活样例）
        EnsureBizSampleMenus(db, adminRoleId, now);
        SeedConfigs(db, now);
        SeedSampleDicts(db, now);
        SeedSampleNotice(db, now);

        // 审批流单据绑定初始值（可运行时在流程管理-单据绑定中调整）
        EnsureFlowBindings(db, now);

        // 存量用户数据归属人回填（幂等）
        db.Ado.ExecuteCommand("UPDATE sys_user SET OwnerUserId = Id WHERE OwnerUserId = 0");

        // 外键列索引：权限查询/会话校验/菜单树是高频路径，避免全表扫描
        EnsureIndex(db, "sys_user_role", "ix_sys_user_role_userid", "UserId");
        EnsureIndex(db, "sys_user_role", "ix_sys_user_role_roleid", "RoleId");
        EnsureIndex(db, "sys_role_menu", "ix_sys_role_menu_roleid", "RoleId");
        EnsureIndex(db, "sys_role_menu", "ix_sys_role_menu_menuid", "MenuId");
        EnsureIndex(db, "sys_menu", "ix_sys_menu_parentid", "ParentId");
        EnsureIndex(db, "sys_user_session", "ix_sys_user_session_tokenid", "TokenId");
        EnsureIndex(db, "sys_user_session", "ix_sys_user_session_userid", "UserId");
        EnsureIndex(db, "sys_user_session", "ix_sys_user_session_refreshtoken", "RefreshTokenHash");
        _logger?.LogInformation("种子数据：索引校验完成");
    }

    /// <summary>
    /// 增量补充「日志管理」菜单（操作日志/登录日志），按权限码幂等：
    /// 已存在则跳过，新菜单自动授予 admin 角色。升级现有库时生效。
    /// </summary>
    private void EnsureLogMenus(ISqlSugarClient db, long adminRoleId, DateTime now)
    {
        var monitorDir = db.Queryable<SysMenu>().First(x => x.MenuName == "系统监控" && x.MenuType == (int)MenuTypeEnum.Directory);
        if (monitorDir == null)
        {
            return; // 全新库走 SeedMenus 完整流程
        }

        (string Name, string Path, string Component, string Permission, int Sort)[] menus =
        [
            ("操作日志", "/monitor/operlog", "monitor/operlog/index", "monitor:operlog:list", 2),
            ("登录日志", "/monitor/loginlog", "monitor/loginlog/index", "monitor:loginlog:list", 3),
            ("变更日志", "/monitor/changelog", "monitor/changelog/index", "monitor:changelog:list", 4)
        ];

        foreach (var spec in menus)
        {
            if (db.Queryable<SysMenu>().Any(x => x.Permission == spec.Permission))
            {
                continue;
            }

            var menu = db.Insertable(new SysMenu
            {
                ParentId = monitorDir.Id,
                MenuName = spec.Name,
                MenuType = (int)MenuTypeEnum.Menu,
                Path = spec.Path,
                Component = spec.Component,
                Permission = spec.Permission,
                Sort = spec.Sort,
                CreateTime = now,
                CreateBy = "system"
            }).ExecuteReturnEntity();

            // 授予 admin 角色（存在性幂等）
            if (!db.Queryable<SysRoleMenu>().Any(x => x.RoleId == adminRoleId && x.MenuId == menu.Id))
            {
                db.Insertable(new SysRoleMenu { Id = NewId(), RoleId = adminRoleId, MenuId = menu.Id, CreateTime = now }).ExecuteCommand();
            }
            _logger?.LogInformation("种子数据：增量菜单「{Name}」已写入", spec.Name);
        }
    }

    /// <summary>
    /// 为指定模块补齐增/改/删按钮权限码（幂等）。
    /// 增量菜单此前只种了 list 权限码，导致写操作全部 403。
    /// </summary>
    private void EnsureCrudButtons(ISqlSugarClient db, string listPermission, long adminRoleId, DateTime now)
    {
        var parent = db.Queryable<SysMenu>().First(x => x.Permission == listPermission);
        if (parent == null)
        {
            return;
        }

        var prefix = listPermission.Replace(":list", string.Empty);
        var buttons = BuildCrudButtons(parent.Id, prefix, now);
        foreach (var button in buttons)
        {
            if (db.Queryable<SysMenu>().Any(x => x.Permission == button.Permission))
            {
                continue;
            }

            var menu = db.Insertable(button).ExecuteReturnEntity();
            if (!db.Queryable<SysRoleMenu>().Any(x => x.RoleId == adminRoleId && x.MenuId == menu.Id))
            {
                db.Insertable(new SysRoleMenu { Id = NewId(), RoleId = adminRoleId, MenuId = menu.Id, CreateTime = now }).ExecuteCommand();
            }
        }
    }

    /// <summary>确保普通（非唯一）索引存在，幂等</summary>
    private static void EnsureIndex(ISqlSugarClient db, string table, string indexName, string column)
    {
        // 表名/列名为代码内常量，无注入风险
        var exists = db.Ado.SqlQuery<int>(
            $"SELECT COUNT(1) FROM sys.indexes WHERE name = '{indexName}' AND object_id = OBJECT_ID('{table}')").First();
        if (exists == 0)
        {
            db.Ado.ExecuteCommand($"CREATE INDEX [{indexName}] ON [{table}] ([{column}])");
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

        var menuMenu = db.Insertable(new SysMenu
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
        }).ExecuteReturnEntity();

        db.Insertable(new SysMenu
        {
            Id = NewId(),
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
        buttons.AddRange(BuildCrudButtons(menuMenu.Id, "sys:menu", now));
        buttons.ForEach(b => b.Id = b.Id == 0 ? Yitter.IdGenerator.YitIdHelper.NextId() : b.Id);
        db.Insertable(buttons).ExecuteCommand();

        // 全部菜单授予 admin 角色
        var menuIds = db.Queryable<SysMenu>().Select(x => x.Id).ToList();
        db.Insertable(menuIds.Select(menuId => new SysRoleMenu
        {
            Id = Yitter.IdGenerator.YitIdHelper.NextId(),
            RoleId = adminRoleId,
            MenuId = menuId,
            CreateTime = now
        }).ToList()).ExecuteCommand();

        // 过滤唯一索引：软删除场景下保证用户名/角色编码唯一（已删除记录不占用）
        EnsureFilteredUniqueIndex(db, "sys_user", "uk_sys_user_username", "UserName");
        EnsureFilteredUniqueIndex(db, "sys_role", "uk_sys_role_rolecode", "RoleCode");
    }

    private static void EnsureFilteredUniqueIndex(ISqlSugarClient db, string table, string indexName, string column)
    {
        // 表名/列名为代码内常量，无注入风险
        var exists = db.Ado.SqlQuery<int>(
            $"SELECT COUNT(1) FROM sys.indexes WHERE name = '{indexName}' AND object_id = OBJECT_ID('{table}')").First();
        if (exists == 0)
        {
            db.Ado.ExecuteCommand($"CREATE UNIQUE INDEX [{indexName}] ON [{table}] ([{column}]) WHERE [IsDeleted] = 0");
        }
    }

    private static List<SysMenu> BuildCrudButtons(long parentId, string permissionPrefix, DateTime now) =>
    [
        new()
        {
            Id = NewId(), ParentId = parentId, MenuName = "新增", MenuType = (int)MenuTypeEnum.Button,
            Permission = $"{permissionPrefix}:add", Sort = 1, CreateTime = now, CreateBy = "system"
        },
        new()
        {
            Id = NewId(), ParentId = parentId, MenuName = "编辑", MenuType = (int)MenuTypeEnum.Button,
            Permission = $"{permissionPrefix}:edit", Sort = 2, CreateTime = now, CreateBy = "system"
        },
        new()
        {
            Id = NewId(), ParentId = parentId, MenuName = "删除", MenuType = (int)MenuTypeEnum.Button,
            Permission = $"{permissionPrefix}:delete", Sort = 3, CreateTime = now, CreateBy = "system"
        }
    ];

    /// <summary>示例部门树（幂等：按编码判断）+ admin 挂根部门 + 部门管理菜单</summary>
    private void SeedDepts(ISqlSugarClient db, long adminRoleId, DateTime now)
    {
        // 部门树
        if (!db.Queryable<SysDept>().Any())
        {
            var root = db.Insertable(new SysDept
            {
            Id = NewId(),
                ParentId = 0, DeptName = "总公司", DeptCode = "HQ", Sort = 1,
                Status = 1, CreateTime = now, CreateBy = "system"
            }).ExecuteReturnEntity();

            var rd = db.Insertable(new SysDept
            {
            Id = NewId(),
                ParentId = root.Id, DeptName = "研发部", DeptCode = "RD", Sort = 1,
                Status = 1, CreateTime = now, CreateBy = "system"
            }).ExecuteReturnEntity();

            db.Insertable(new SysDept
            {
                Id = NewId(),
                ParentId = root.Id, DeptName = "市场部", DeptCode = "MKT", Sort = 2,
                Status = 1, CreateTime = now, CreateBy = "system"
            }).ExecuteCommand();

            db.Insertable(new SysDept
            {
                Id = NewId(),
                ParentId = rd.Id, DeptName = "研发一组", DeptCode = "RD1", Sort = 1,
                Status = 1, CreateTime = now, CreateBy = "system"
            }).ExecuteCommand();

            db.Insertable(new SysDept
            {
                Id = NewId(),
                ParentId = rd.Id, DeptName = "研发二组", DeptCode = "RD2", Sort = 2,
                Status = 1, CreateTime = now, CreateBy = "system"
            }).ExecuteCommand();

            _logger?.LogInformation("种子数据：示例部门树已写入");
        }

        // admin 挂根部门（存量库升级补挂）
        var rootDept = db.Queryable<SysDept>().First(x => x.DeptCode == "HQ");
        var adminUser = db.Queryable<SysUser>().First(x => x.UserName == "admin");
        if (rootDept != null && adminUser != null && adminUser.DeptId != rootDept.Id)
        {
            db.Updateable<SysUser>()
                .SetColumns("DeptId", rootDept.Id)
                .Where(x => x.Id == adminUser.Id)
                .ExecuteCommand();
        }

        // 部门管理菜单（增量）
        if (db.Queryable<SysMenu>().Any(x => x.Permission == "sys:dept:list"))
        {
            return;
        }
        var systemDir = db.Queryable<SysMenu>().First(x => x.MenuName == "系统管理" && x.MenuType == (int)MenuTypeEnum.Directory);
        if (systemDir == null)
        {
            return;
        }
        var menu = db.Insertable(new SysMenu
        {
            ParentId = systemDir.Id,
            MenuName = "部门管理",
            MenuType = (int)MenuTypeEnum.Menu,
            Path = "/system/dept",
            Component = "system/dept/index",
            Permission = "sys:dept:list",
            Sort = 7,
            CreateTime = now,
            CreateBy = "system"
        }).ExecuteReturnEntity();
        db.Insertable(BuildCrudButtons(menu.Id, "sys:dept", now)).ExecuteCommand();
        var menuIds = new List<long> { menu.Id };
        menuIds.AddRange(db.Queryable<SysMenu>().Where(x => x.ParentId == menu.Id).Select(x => x.Id).ToList());
        db.Insertable(menuIds.Select(menuId => new SysRoleMenu { Id = NewId(), RoleId = adminRoleId, MenuId = menuId, CreateTime = now }).ToList()).ExecuteCommand();
        _logger?.LogInformation("种子数据：增量菜单「部门管理」已写入");
    }

    /// <summary>增量补充「定时任务」菜单（按权限码幂等）</summary>
    private void EnsureJobMenu(ISqlSugarClient db, long adminRoleId, DateTime now)
    {
        var monitorDir = db.Queryable<SysMenu>().First(x => x.MenuName == "系统监控" && x.MenuType == (int)MenuTypeEnum.Directory);
        if (monitorDir == null || db.Queryable<SysMenu>().Any(x => x.Permission == "monitor:job:list"))
        {
            return;
        }

        var menu = db.Insertable(new SysMenu
        {
            Id = NewId(),
            ParentId = monitorDir.Id,
            MenuName = "定时任务",
            MenuType = (int)MenuTypeEnum.Menu,
            Path = "/monitor/job",
            Component = "monitor/job/index",
            Permission = "monitor:job:list",
            Sort = 4,
            CreateTime = now,
            CreateBy = "system"
        }).ExecuteReturnEntity();

        var editMenu = db.Insertable(new SysMenu
        {
            Id = NewId(),
            ParentId = menu.Id,
            MenuName = "任务管理",
            MenuType = (int)MenuTypeEnum.Button,
            Permission = "monitor:job:edit",
            Sort = 1,
            CreateTime = now,
            CreateBy = "system"
        }).ExecuteReturnEntity();

        var triggerMenu = db.Insertable(new SysMenu
        {
            Id = NewId(),
            ParentId = menu.Id,
            MenuName = "任务触发",
            MenuType = (int)MenuTypeEnum.Button,
            Permission = "monitor:job:trigger",
            Sort = 2,
            CreateTime = now,
            CreateBy = "system"
        }).ExecuteReturnEntity();

        db.Insertable(new[]
        {
            new SysRoleMenu { Id = NewId(), RoleId = adminRoleId, MenuId = menu.Id, CreateTime = now },
            new SysRoleMenu { Id = NewId(), RoleId = adminRoleId, MenuId = editMenu.Id, CreateTime = now },
            new SysRoleMenu { Id = NewId(), RoleId = adminRoleId, MenuId = triggerMenu.Id, CreateTime = now }
        }).ExecuteCommand();
        _logger?.LogInformation("种子数据：增量菜单「定时任务」已写入");
    }

    /// <summary>增量补充「业务办公」目录与报销/采购申请页（审批流业务样板，幂等）</summary>
    private void EnsureBizSampleMenus(ISqlSugarClient db, long adminRoleId, DateTime now)
    {
        const string expensePermission = "biz:expense:list";
        if (db.Queryable<SysMenu>().Any(x => x.Permission == expensePermission))
        {
            EnsureCrudButtons(db, "biz:expense:list", adminRoleId, now);
            EnsureCrudButtons(db, "biz:purchase:list", adminRoleId, now);
            return;
        }

        var bizDir = db.Queryable<SysMenu>().First(x => x.MenuName == "业务办公" && x.MenuType == (int)MenuTypeEnum.Directory);
        if (bizDir == null)
        {
            bizDir = db.Insertable(new SysMenu
            {
                ParentId = 0,
                MenuName = "业务办公",
                MenuType = (int)MenuTypeEnum.Directory,
                Path = "/biz",
                Icon = "Files",
                Sort = 6,
                CreateTime = now,
                CreateBy = "system"
            }).ExecuteReturnEntity();
        }

        (string Name, string Path, string Component, string Permission, int Sort)[] pages =
        [
            ("报销管理", "/biz/expense", "biz/expense/index", expensePermission, 1),
            ("采购申请", "/biz/purchase", "biz/purchase/index", "biz:purchase:list", 2)
        ];

        foreach (var spec in pages)
        {
            if (db.Queryable<SysMenu>().Any(x => x.Permission == spec.Permission))
            {
                continue;
            }

            var menu = db.Insertable(new SysMenu
            {
                ParentId = bizDir.Id,
                MenuName = spec.Name,
                MenuType = (int)MenuTypeEnum.Menu,
                Path = spec.Path,
                Component = spec.Component,
                Permission = spec.Permission,
                Sort = spec.Sort,
                CreateTime = now,
                CreateBy = "system"
            }).ExecuteReturnEntity();

            if (!db.Queryable<SysRoleMenu>().Any(x => x.RoleId == adminRoleId && x.MenuId == menu.Id))
            {
                db.Insertable(new SysRoleMenu { Id = NewId(), RoleId = adminRoleId, MenuId = menu.Id, CreateTime = now }).ExecuteCommand();
            }
        }
        EnsureCrudButtons(db, "biz:expense:list", adminRoleId, now);
        EnsureCrudButtons(db, "biz:purchase:list", adminRoleId, now);
        _logger?.LogInformation("种子数据：增量菜单「业务办公/报销管理/采购申请」已写入");
    }

    /// <summary>
    /// <summary>审批流单据绑定初始值（幂等：按业务表名判断）</summary>
    private static void EnsureFlowBindings(ISqlSugarClient db, DateTime now)
    {
        (string Table, string Code, string Remark)[] bindings =
        [
            ("biz_expense", "expense", "报销单"),
            ("biz_purchase_request", "purchase_request", "采购申请单")
        ];
        foreach (var (table, code, remark) in bindings)
        {
            if (db.Queryable<SysFlowBinding>().Any(x => x.BusinessTable == table))
            {
                continue;
            }
            // 种子直连路径 AOP 雪花不可靠，必须显式填 Id（其余种子同此约定）
            db.Insertable(new SysFlowBinding
            {
                Id = NewId(),
                BusinessTable = table,
                FlowCode = code,
                Remark = remark,
                CreateTime = now,
                CreateBy = "system"
            }).ExecuteCommand();
        }
    }

    /// 增量补充「个人办公」目录与我的待办/已办/我的申请页（幂等，个人菜单无权限码）。
    /// 注意：目录哨兵码不得与任何页面的权限码相同——曾因目录与待办页共用哨兵导致查重误跳过待办页。
    /// </summary>
    private void EnsurePersonalFlowMenus(ISqlSugarClient db, long adminRoleId, DateTime now)
    {
        // 目录哨兵（独立值，仅用于幂等判断，不作为功能权限）
        const string dirSentinel = "menu:personal:dir";

        var personalDir = db.Queryable<SysMenu>().First(x => x.MenuName == "个人办公" && x.MenuType == (int)MenuTypeEnum.Directory);
        if (personalDir == null)
        {
            personalDir = db.Insertable(new SysMenu
            {
                ParentId = 0,
                MenuName = "个人办公",
                MenuType = (int)MenuTypeEnum.Directory,
                Path = "/personal",
                Icon = "Checked",
                Sort = 5,
                Permission = dirSentinel,
                CreateTime = now,
                CreateBy = "system"
            }).ExecuteReturnEntity();
        }
        else if (personalDir.Permission == "menu:flow:todo")
        {
            // 历史版本目录哨兵与待办页权限码相同（导致待办页被查重跳过），迁移哨兵值
            db.Updateable<SysMenu>()
                .SetColumns("Permission", dirSentinel)
                .Where(x => x.Id == personalDir.Id)
                .ExecuteCommand();
        }

        (string Name, string Path, string Component, string Permission, int Sort)[] pages =
        [
            ("我的待办", "/personal/todo", "flow/todo/index", "menu:flow:todo", 1),
            ("我的已办", "/personal/done", "flow/done/index", "menu:flow:done", 2),
            ("我的申请", "/personal/mine", "flow/mine/index", "menu:flow:mine", 3)
        ];

        foreach (var spec in pages)
        {
            if (db.Queryable<SysMenu>().Any(x => x.Permission == spec.Permission))
            {
                continue;
            }

            var menu = db.Insertable(new SysMenu
            {
                ParentId = personalDir.Id,
                MenuName = spec.Name,
                MenuType = (int)MenuTypeEnum.Menu,
                Path = spec.Path,
                Component = spec.Component,
                Permission = spec.Permission,
                Sort = spec.Sort,
                CreateTime = now,
                CreateBy = "system"
            }).ExecuteReturnEntity();

            // 授予 admin 角色（存在性幂等）；普通角色由管理员按需分配菜单
            if (!db.Queryable<SysRoleMenu>().Any(x => x.RoleId == adminRoleId && x.MenuId == menu.Id))
            {
                db.Insertable(new SysRoleMenu { Id = NewId(), RoleId = adminRoleId, MenuId = menu.Id, CreateTime = now }).ExecuteCommand();
            }
        }
        _logger?.LogInformation("种子数据：增量菜单「个人办公/我的待办/我的已办」已写入");
    }

    /// <summary>增量补充「流程管理」菜单（按权限码幂等，审批流定义管理页）</summary>
    private void EnsureFlowMenu(ISqlSugarClient db, long adminRoleId, DateTime now)
    {
        var systemDir = db.Queryable<SysMenu>().First(x => x.MenuName == "系统管理" && x.MenuType == (int)MenuTypeEnum.Directory);
        if (systemDir == null)
        {
            return;
        }

        const string permission = "sys:flow:list";
        if (db.Queryable<SysMenu>().Any(x => x.Permission == permission))
        {
            return;
        }

        var menu = db.Insertable(new SysMenu
        {
            ParentId = systemDir.Id,
            MenuName = "流程管理",
            MenuType = (int)MenuTypeEnum.Menu,
            Path = "/system/flow",
            Component = "system/flow/index",
            Permission = permission,
            Sort = 7,
            CreateTime = now,
            CreateBy = "system"
        }).ExecuteReturnEntity();

        if (!db.Queryable<SysRoleMenu>().Any(x => x.RoleId == adminRoleId && x.MenuId == menu.Id))
        {
            db.Insertable(new SysRoleMenu { Id = NewId(), RoleId = adminRoleId, MenuId = menu.Id, CreateTime = now }).ExecuteCommand();
        }
        _logger?.LogInformation("种子数据：增量菜单「流程管理」已写入");
    }

    /// <summary>增量补充「服务监控」菜单（按权限码幂等）</summary>
    private void EnsureMonitorMenu(ISqlSugarClient db, long adminRoleId, DateTime now)
    {
        var monitorDir = db.Queryable<SysMenu>().First(x => x.MenuName == "系统监控" && x.MenuType == (int)MenuTypeEnum.Directory);
        if (monitorDir == null || db.Queryable<SysMenu>().Any(x => x.Permission == "monitor:system:list"))
        {
            return;
        }

        var menu = db.Insertable(new SysMenu
        {
            ParentId = monitorDir.Id,
            MenuName = "服务监控",
            MenuType = (int)MenuTypeEnum.Menu,
            Path = "/monitor/system",
            Component = "monitor/system/index",
            Permission = "monitor:system:list",
            Sort = 3,
            CreateTime = now,
            CreateBy = "system"
        }).ExecuteReturnEntity();

        if (!db.Queryable<SysRoleMenu>().Any(x => x.RoleId == adminRoleId && x.MenuId == menu.Id))
        {
            db.Insertable(new SysRoleMenu { Id = NewId(), RoleId = adminRoleId, MenuId = menu.Id, CreateTime = now }).ExecuteCommand();
        }
        _logger?.LogInformation("种子数据：增量菜单「服务监控」已写入");
    }

    private static long NewId() => Yitter.IdGenerator.YitIdHelper.NextId();

    /// <summary>内置系统参数（幂等：按参数键判断）</summary>
    private void SeedConfigs(ISqlSugarClient db, DateTime now)
    {
        (string Key, string Value, string Name, string Remark)[] configs =
        [
            ("sys.pwd.defaultPassword", "Net123456", "默认初始密码", "新建用户/重置密码使用的默认密码，需满足密码策略"),
            ("sys.login.failThreshold", "5", "登录失败锁定阈值", "连续失败达到该次数后锁定账号"),
            ("sys.login.lockMinutes", "10", "登录锁定时长(分钟)", "账号锁定持续时间"),
            ("sys.captcha.enabled", "true", "登录图形验证码开关", "设为 false 关闭验证码（内网场景）")
        ];

        foreach (var spec in configs)
        {
            if (db.Queryable<SysConfig>().Any(x => x.ConfigKey == spec.Key))
            {
                continue;
            }
            db.Insertable(new SysConfig
            {
                Id = NewId(),
                ConfigKey = spec.Key,
                ConfigValue = spec.Value,
                ConfigName = spec.Name,
                IsBuiltIn = true,
                Remark = spec.Remark,
                CreateTime = now,
                CreateBy = "system"
            }).ExecuteCommand();
            _logger?.LogInformation("种子数据：内置参数 {Key} 已写入", spec.Key);
        }
    }

    /// <summary>增量补充「通知公告」菜单（按权限码幂等）</summary>
    private void EnsureNoticeMenu(ISqlSugarClient db, long adminRoleId, DateTime now)
    {
        var systemDir = db.Queryable<SysMenu>().First(x => x.MenuName == "系统管理" && x.MenuType == (int)MenuTypeEnum.Directory);
        if (systemDir == null || db.Queryable<SysMenu>().Any(x => x.Permission == "sys:notice:list"))
        {
            return;
        }

        var menu = db.Insertable(new SysMenu
        {
            ParentId = systemDir.Id,
            MenuName = "通知公告",
            MenuType = (int)MenuTypeEnum.Menu,
            Path = "/system/notice",
            Component = "system/notice/index",
            Permission = "sys:notice:list",
            Sort = 6,
            CreateTime = now,
            CreateBy = "system"
        }).ExecuteReturnEntity();

        if (!db.Queryable<SysRoleMenu>().Any(x => x.RoleId == adminRoleId && x.MenuId == menu.Id))
        {
            db.Insertable(new SysRoleMenu { Id = NewId(), RoleId = adminRoleId, MenuId = menu.Id, CreateTime = now }).ExecuteCommand();
        }
        _logger?.LogInformation("种子数据：增量菜单「通知公告」已写入");
    }

    /// <summary>示例公告（幂等）</summary>
    private void SeedSampleNotice(ISqlSugarClient db, DateTime now)
    {
        if (db.Queryable<SysNotice>().Any())
        {
            return;
        }
        db.Insertable(new SysNotice
        {
            Id = NewId(),
            Title = "欢迎使用 NetBase 管理系统",
            NoticeType = 1,
            Content = "框架已内置：用户/角色/菜单/字典/参数/公告管理、操作与登录审计、Excel 导入导出、登录验证码与防重复提交。本条为示例公告，可在通知公告管理中维护。",
            Status = 1,
            CreateTime = now,
            CreateBy = "system"
        }).ExecuteCommand();
    }

    /// <summary>示例字典（幂等：按字典编码判断）</summary>
    private void SeedSampleDicts(ISqlSugarClient db, DateTime now)
    {
        if (db.Queryable<SysDictType>().Any(x => x.DictCode == "demo_priority"))
        {
            return;
        }

        var type = db.Insertable(new SysDictType
        {
            DictCode = "demo_priority",
            DictName = "优先级（示例）",
            Status = 1,
            Remark = "框架自带的字典使用示例，业务字典照此模式维护",
            CreateTime = now,
            CreateBy = "system"
        }).ExecuteReturnEntity();

        (string Label, string Value, int Sort)[] items =
        [
            ("高", "1", 1),
            ("中", "2", 2),
            ("低", "3", 3)
        ];
        db.Insertable(items.Select(i => new SysDictData
        {
            Id = Yitter.IdGenerator.YitIdHelper.NextId(),
            DictTypeId = type.Id,
            Label = i.Label,
            Value = i.Value,
            Sort = i.Sort,
            Status = 1,
            CreateTime = now,
            CreateBy = "system"
        }).ToList()).ExecuteCommand();
        _logger?.LogInformation("种子数据：示例字典 demo_priority 已写入");
    }

    /// <summary>增量补充「系统管理」下的字典/参数菜单（按权限码幂等）</summary>
    private void EnsureSystemMenus(ISqlSugarClient db, long adminRoleId, DateTime now)
    {
        var systemDir = db.Queryable<SysMenu>().First(x => x.MenuName == "系统管理" && x.MenuType == (int)MenuTypeEnum.Directory);
        if (systemDir == null)
        {
            return;
        }

        (string Name, string Path, string Component, string Permission, int Sort)[] menus =
        [
            ("字典管理", "/system/dict", "system/dict/index", "sys:dict:list", 4),
            ("参数配置", "/system/config", "system/config/index", "sys:config:list", 5)
        ];

        foreach (var spec in menus)
        {
            if (db.Queryable<SysMenu>().Any(x => x.Permission == spec.Permission))
            {
                continue;
            }

            var menu = db.Insertable(new SysMenu
            {
                ParentId = systemDir.Id,
                MenuName = spec.Name,
                MenuType = (int)MenuTypeEnum.Menu,
                Path = spec.Path,
                Component = spec.Component,
                Permission = spec.Permission,
                Sort = spec.Sort,
                CreateTime = now,
                CreateBy = "system"
            }).ExecuteReturnEntity();

            if (!db.Queryable<SysRoleMenu>().Any(x => x.RoleId == adminRoleId && x.MenuId == menu.Id))
            {
                db.Insertable(new SysRoleMenu { Id = NewId(), RoleId = adminRoleId, MenuId = menu.Id, CreateTime = now }).ExecuteCommand();
            }
            _logger?.LogInformation("种子数据：增量菜单「{Name}」已写入", spec.Name);
        }
    }}

