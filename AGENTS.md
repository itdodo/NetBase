# AGENTS.md — AI 协作规范入口

本文件是 AI 工具（ZCode/Claude/Copilot 等）在本仓库工作时**必须先阅读的入口指引**。

## 开工前必读

1. [docs/技术文档.md](docs/技术文档.md) —— 技术栈、架构机制（泛型三层/认证授权/审计/数据权限/雪花ID）、配置速查、开发约定
2. [docs/业务开发规范.md](docs/业务开发规范.md) —— 业务分支开发必须遵守：目录边界、建表规范、各层模板、AI 协作约定
3. [docs/框架蓝图.md](docs/框架蓝图.md) —— 框架重建蓝图：技术选型/架构规范/语义清单/**踩坑红线**（改造前必读）

## 硬性规则（违反即返工）

1. **目录边界**：框架层（`Sys` 表前缀、`Sys` 服务/控制器）与业务层（`Biz` 目录、`biz_` 表前缀）物理隔离，禁止业务代码进框架目录
2. **雪花 ID**：主键 long 非自增，插入时框架自动填充；返回前端的雪花 ID 字段必须标 `LongToStringConverter`；前端 id 类型一律 string
3. **审计**：CreateBy/CreateTime/UpdateTime/UpdateBy 由 AOP 自动填充，业务代码无需手动赋值
4. **事务**：多表操作必须 `Repository.TransactionAsync` 包裹
5. **权限**：每个接口 `[HasPermission("biz:{模块}:{动作}")]`；创建类接口加 `[NoRepeatSubmit]`
6. **验证**：DTO 必须带 DataAnnotations 特性；密码强度走 `PasswordPolicy.Validate`
7. **数据库 = PostgreSQL**：连接串 `Host=...;Database=...;Username=...;Password=...`；原生 SQL 用 PG 方言+小写物理列名；裸 Insertable 必须显式 `Id = NewId()`（PG 下 AOP 对主键不生效）
8. **错误码**：业务异常必须查码表赋码 `throw new BusinessException("文案", ErrorCodes.XXX)`（码表 `NetBase.Common/Results/ErrorCodes.cs`，总表 [docs/错误码.md](docs/错误码.md)）；码只增不改不删；后端加码后运行 `node scripts/gen-error-codes-ts.cjs` 同步前端镜像
9. **数据库变更**：实体加列走 CodeFirst 自动同步；数据回填/索引调优/需评审的 DDL 必须新增 `src/NetBase.Repository/db/migrations/NNNN_描述.sql`（幂等、禁改历史脚本）
10. **交付自检**：`dotnet build` 0 错误 → `dotnet test`（单元 58 + 集成 71）全过 → `npm run type-check` 零错误 → 关键路径实测 → 同步更新 docs

## 常用命令

```bash
dotnet build                                # 后端构建（0 警告 0 错误基线）
dotnet test                                 # 全部测试（58 单元 + 71 集成）
cd web && npm run type-check                # 前端类型检查
cd web && npm run dev                       # （仅前端联调时）开发前端；日常走 Docker 形态 8680
docker compose -p netbase up -d --build    # 日常全栈：前端+API+PG 全在 Docker（UI=8680）
```

## 提醒

- 集成测试连专用 PostgreSQL 库 `netbase_test`（netbase-pg 容器 5433，自动建库建表）；CI 中用 `NETBASE_TEST_CONNECTIONSTRING` 覆盖
- 新增/修改/删除功能、表、接口时，同步更新 `docs/技术文档.md` 变更记录
