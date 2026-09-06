# NetBase 通用基础框架

基于 **.NET 10 (C#)** 的企业级 Web 开发基础框架，采用传统三层架构（Service + Repository）并引入泛型化设计，内置 RBAC 权限数据模型，ORM 使用 SqlSugarCore（最新稳定版），数据库为 SqlServer。满足中小型企业 Web 开发需求；前端采用 Vue 3 + Element Plus（`web/` 目录），开箱即得完整的管理系统骨架。

## 技术栈

| 分类 | 技术 | 说明 |
|---|---|---|
| 运行时 | .NET 10 | C# 最新语言特性 |
| ORM | SqlSugarCore 5.1.4.x | CodeFirst、全局软删除过滤器、AOP SQL 日志 |
| 数据库 | SqlServer | 连接串在 appsettings.json 配置 |
| 缓存 | MemoryCache（默认）/ Redis（备用） | 配置一键切换 |
| 消息队列 | RabbitMQ（备用，默认关闭） | RabbitMQ.Client 7.x 异步 API |
| 日志 | Serilog | 控制台 + 按日滚动文件（Logs/） |
| API 文档 | .NET 10 内置 OpenAPI + Swagger UI + XML 注释 | `/swagger`（Bearer 调试） |
| 认证 | JWT Bearer + RefreshToken 轮换 + 会话表 | 登录锁定/强制下线/在线用户 |
| 审计 | 操作日志 + 登录日志（参数脱敏） | 等保要求 |
| Excel | MiniExcel | 列表导出 |
| 部署 | Dockerfile + docker-compose | 前端静态文件由 API 托管，单容器 |
| 文件上传 | 本地存储（白名单/大小校验），预留 OSS 切换 | 头像等 |
| 安全增强 | 登录图形验证码（可参数开关）+ 防重复提交（2 秒窗口判重） | |
| 通知公告 | 发布/铃铛提醒/详情查看 | |
| 用户导入 | Excel 模板下载 + 批量导入（逐行校验回显） | |

## 解决方案结构

```
NetBase.slnx
├── web/                      前端工程（Vue 3 + TypeScript + Vite + Element Plus）
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

### 1. 启动后端

修改 `src/NetBase.Api/appsettings.json` 中的数据库连接串：

```json
"Db": {
  "ConnectionString": "Server=localhost;Database=NetBase;Uid=sa;Pwd=你的密码;TrustServerCertificate=True;",
  "InitEnabled": true
}
```

```bash
cd src/NetBase.Api
dotnet run          # 默认 http://localhost:5026；Swagger 见 /swagger
```

首次启动会自动 **CodeFirst 建表并写入种子数据**（`Db:InitEnabled=false` 可关闭）：

- 账号：`admin` / 密码：`123456`（历史库存量账号；新建用户默认密码为系统参数 `sys.pwd.defaultPassword`，当前值 `Net123456`，满足密码策略）
- 登录保护：连续失败 5 次锁定 10 分钟（系统参数可调），每 IP 每分钟限 10 次尝试
- 角色：`超级管理员（admin）`
- 菜单：系统管理（用户/角色/菜单 + 增删改按钮权限）、系统监控

> 注意：本机无 SqlServer 时应用仍可启动（初始化失败仅记录错误日志），但数据库接口不可用。

### 2. 启动前端

需要 Node.js 20.19+：

```bash
cd web
npm install
npm run dev         # http://localhost:5173，已配置 /api 代理到后端 5026
```

登录页当前为**本地模拟登录**（任意账号密码可进入，JWT 待接入），登录后自动拉取
`/api/sys/menu/tree` 生成侧边栏与动态路由，用户/角色/菜单三个管理页直接联调真实接口。

## RBAC 数据模型

经典五表结构（`src/NetBase.Model/Entities`）：

```
SysUser ──< SysUserRole >── SysRole ──< SysRoleMenu >── SysMenu(树形:目录/菜单/按钮)
```

- 用户/角色均内置保护：`admin` 账号与 `admin` 角色不允许删除、停用
- 菜单按钮类型带 `Permission` 权限码（如 `sys:user:add`），为后续接口鉴权预留
- 所有业务表继承 `BaseEntity`：自增主键、创建/更新审计字段、`ISoftDelete` 软删除（查询自动过滤）

## API 版本

所有接口路径带版本段：`/api/v1/...`，配合 Swagger XML 注释（接口/DTO 说明自动进文档）。

## 数据权限模型

业务实体实现 `IDataScope`（DeptId + OwnerUserId 列）即自动纳入数据权限过滤：

```
角色.DataScope: 1全部 2自定义(SysRoleDept勾选) 3本部门 4本部门及以下 5仅本人
多角色取并集（部门集合 ∪ 本人数据），请求级过滤器注入，业务代码零侵入
```

## API 一览（/api/v1）

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

## 前端架构（web/）

自建轻量版管理系统骨架，无模板冗余：

```
web/src/
├── api/            request.ts（axios 统一封装）+ user/role/menu 模块
├── stores/         user（登录态）、permission（菜单树/权限码/动态路由）
├── router/         静态路由 + 登录守卫（拉菜单 → addRoute 动态注册）
├── layout/         主布局（侧边栏递归菜单、顶栏、面包屑）
├── directives/     v-permission 按钮级权限指令
├── views/          login、dashboard、system/{user,role,menu}、error/404
└── types/          与后端 DTO 对齐的 TS 类型
```

关键机制：

- **动态菜单路由**：登录后拉取菜单树，`component` 字段（如 `system/user/index`）通过
  `import.meta.glob` 映射到 `views/` 下同名页面组件，新页面只需按约定建目录 + 在菜单管理里配菜单
- **权限码**：菜单树中所有 `permission` 收集为集合，`v-permission="'sys:user:add'"` 控制按钮显隐
- **认证预留**：token 由 request.ts 统一注入 `Authorization: Bearer`，401 自动跳登录；
  接入 JWT 时仅需替换 `stores/user.ts` 中 login 的 mock 实现
- **降级策略**：菜单接口不可用时提示并以基础模式进入（仅首页），不白屏

## Docker 部署

```bash
docker compose up -d        # 单容器：API 托管前端静态文件 + SqlServer，自动建表种子
# 访问 http://localhost:8080（生产环境务必覆盖 Jwt__SecretKey 与数据库密码环境变量）
```

本地生产模式验证：`cd web && npm run build`，将 `web/dist` 复制到 `src/NetBase.Api/wwwroot/` 后 `dotnet run`。

## 后续扩展点（按需求演进）

1. **部门/组织架构 + 数据权限**（本部门/本部门及以下/全部）——企业 RBAC 的第二权限维度
2. **代码生成器**（三层样板生成）、文件上传、通知公告、定时任务
3. **多租户、国际化、业务错误码体系**
4. **Redis/RabbitMQ**：后端已封装（`ICacheService`、`IRabbitMqPublisher`），配置开关即用
