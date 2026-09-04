# NetBase 通用基础框架

基于 **.NET 10 (C#)** 的企业级 Web 开发基础框架，采用传统三层架构（Service + Repository）并引入泛型化设计，内置 RBAC 权限数据模型，ORM 使用 SqlSugarCore（最新稳定版），数据库为 SqlServer。满足中小型企业 Web 开发需求；UI 层待定，当前提供标准 REST API 作为后续 UI 的对接基础。

## 技术栈

| 分类 | 技术 | 说明 |
|---|---|---|
| 运行时 | .NET 10 | C# 最新语言特性 |
| ORM | SqlSugarCore 5.1.4.x | CodeFirst、全局软删除过滤器、AOP SQL 日志 |
| 数据库 | SqlServer | 连接串在 appsettings.json 配置 |
| 缓存 | MemoryCache（默认）/ Redis（备用） | 配置一键切换 |
| 消息队列 | RabbitMQ（备用，默认关闭） | RabbitMQ.Client 7.x 异步 API |
| 日志 | Serilog | 控制台 + 按日滚动文件（Logs/） |
| API 文档 | .NET 10 内置 OpenAPI + Swagger UI | `/swagger` |

## 解决方案结构

```
NetBase.slnx
└── src/
    ├── NetBase.Api/          API 宿主层：Controllers、全局过滤器、Program.cs
    ├── NetBase.Service/      业务逻辑层：BaseService<T> 泛型服务 + 用户/角色/菜单服务
    ├── NetBase.Repository/   数据访问层：SqlSugar 配置、IRepository<T> 泛型仓储、DbSeeder
    ├── NetBase.Model/        实体层：RBAC 五表实体、DTO、枚举
    ├── NetBase.Common/       通用层：ApiResult 统一返回、BusinessException、密码工具、缓存接口
    └── NetBase.Middleware/   中间件封装（备用）：缓存实现、RabbitMQ 发布器
```

依赖方向：`Api → Middleware → Service → Repository → Model → Common`。

## 快速开始

1. 修改 `src/NetBase.Api/appsettings.json` 中的数据库连接串：

```json
"Db": {
  "ConnectionString": "Server=localhost;Database=NetBase;Uid=sa;Pwd=你的密码;TrustServerCertificate=True;",
  "InitEnabled": true
}
```

2. 运行：

```bash
cd src/NetBase.Api
dotnet run
```

3. 打开 Swagger 调试：`https://localhost:7090/swagger`（或启动日志中显示的地址）。

首次启动会自动 **CodeFirst 建表并写入种子数据**（`Db:InitEnabled=false` 可关闭）：

- 账号：`admin` / 密码：`123456`
- 角色：`超级管理员（admin）`
- 菜单：系统管理（用户/角色/菜单 + 增删改按钮权限）、系统监控

> 注意：本机无 SqlServer 时应用仍可启动（初始化失败仅记录错误日志），但数据库接口不可用。

## RBAC 数据模型

经典五表结构（`src/NetBase.Model/Entities`）：

```
SysUser ──< SysUserRole >── SysRole ──< SysRoleMenu >── SysMenu(树形:目录/菜单/按钮)
```

- 用户/角色均内置保护：`admin` 账号与 `admin` 角色不允许删除、停用
- 菜单按钮类型带 `Permission` 权限码（如 `sys:user:add`），为后续接口鉴权预留
- 所有业务表继承 `BaseEntity`：自增主键、创建/更新审计字段、`ISoftDelete` 软删除（查询自动过滤）

## API 一览（/api/sys）

| 模块 | 方法与路由 | 说明 |
|---|---|---|
| 用户 | `GET /user/page`、`GET /user/list`、`GET /user/{id}` | 分页（关键字/状态过滤）、下拉、详情（含角色） |
| 用户 | `POST /user`、`PUT /user/{id}`、`DELETE /user/{id}` | 增改删（创建传 RoleIds 即分配角色） |
| 用户 | `PUT /user/{id}/password/reset`、`PUT /user/{id}/roles` | 重置密码、分配角色（全量重设） |
| 角色 | `GET /role/page`、`GET /role/list`、`GET /role/{id}` | 分页、下拉、详情 |
| 角色 | `POST /role`、`PUT /role/{id}`、`DELETE /role/{id}` | 增改删（RoleCode 唯一） |
| 角色 | `GET /role/{id}/menu-ids`、`PUT /role/{id}/menus` | 查询已授权菜单、分配菜单 |
| 菜单 | `GET /menu/tree`、`GET /menu/tree/role/{roleId}` | 全量菜单树、指定角色菜单树 |
| 菜单 | `GET /menu/{id}`、`POST /menu`、`PUT /menu/{id}`、`DELETE /menu/{id}` | 详情、增改删（父级/子节点/引用校验） |
| 系统 | `GET /health` | 健康检查 |

统一返回格式：`{ "code": 200, "message": "操作成功", "data": ..., "timestamp": ... }`；
业务错误由 `BusinessException` 抛出，全局异常过滤器统一转换为 ApiResult（不泄露堆栈）。

## 扩展新业务模块（三层样板）

以"订单"为例：

1. **Model**：`Entities/Order.cs : BaseEntity` + `Dtos/OrderDtos.cs`
2. **Repository**：无需写代码，直接注入 `IRepository<Order>`（泛型开放注册）；复杂查询用 `repository.Queryable`
3. **Service**：`IOrderService` + `OrderService : BaseService<Order>, IOrderService`，在 `NetBase.Service/ServiceCollectionExtensions.cs` 注册
4. **Api**：`OrderController : BaseController`，注入 `IOrderService`

## 配置说明（appsettings.json）

| 节点 | 说明 | 默认 |
|---|---|---|
| `Db:ConnectionString` | SqlServer 连接串 | localhost 示例值，必须修改 |
| `Db:InitEnabled` | 启动时建表+种子数据 | `true` |
| `Db:LogSql` / `Db:SlowSqlThresholdMs` | SQL 日志 / 慢查询阈值 | `true` / `3000` |
| `Cache:Provider` | `Memory`（默认）或 `Redis` | `Memory` |
| `Cache:RedisConnectionString` | Redis 连接串（Provider=Redis 时启用） | localhost:6379 |
| `RabbitMQ:Enabled` | 启用 MQ 发布器注册 | `false` |

## 后续扩展点（按需求演进）

1. **认证授权**：接入 JWT Bearer + 权限过滤器（读取 `SysMenu.Permission` 权限码校验接口权限）；`PasswordHelper` 建议升级为 BCrypt/PBKDF2
2. **UI 层**：Vue/React 前端对接 `/api/sys` 接口与菜单树接口
3. **Redis/RabbitMQ**：已封装（`ICacheService`、`IRabbitMqPublisher`），配置开关即用
4. **多租户/审计日志/操作日志**：可在 `BaseEntity` 与 AOP 上扩展
