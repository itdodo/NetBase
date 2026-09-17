# AGENTS.md — AI 协作规范入口

本文件是 AI 工具（ZCode/Claude/Copilot 等）在本仓库工作时**必须先阅读的入口指引**。

## 开工前必读

1. [docs/技术文档.md](docs/技术文档.md) —— 技术栈、架构机制（泛型三层/认证授权/审计/数据权限/雪花ID）、配置速查、开发约定
2. [docs/业务开发规范.md](docs/业务开发规范.md) —— 业务分支开发必须遵守：目录边界、建表规范、各层模板、AI 协作约定

## 硬性规则（违反即返工）

1. **目录边界**：框架层（`Sys` 表前缀、`Sys` 服务/控制器）与业务层（`Biz` 目录、`biz_` 表前缀）物理隔离，禁止业务代码进框架目录
2. **雪花 ID**：主键 long 非自增，插入时框架自动填充；返回前端的雪花 ID 字段必须标 `LongToStringConverter`；前端 id 类型一律 string
3. **审计**：CreateBy/CreateTime/UpdateTime/UpdateBy 由 AOP 自动填充，业务代码无需手动赋值
4. **事务**：多表操作必须 `Repository.TransactionAsync` 包裹
5. **权限**：每个接口 `[HasPermission("biz:{模块}:{动作}")]`；创建类接口加 `[NoRepeatSubmit]`
6. **验证**：DTO 必须带 DataAnnotations 特性；密码强度走 `PasswordPolicy.Validate`
7. **数据库变更**：实体加列走 CodeFirst 自动同步；数据回填/索引调优/需评审的 DDL 必须新增 `src/NetBase.Repository/db/migrations/NNNN_描述.sql`（幂等、禁改历史脚本）
8. **交付自检**：`dotnet build` 0 错误 → `dotnet test`（单元 37 + 集成 43）全过 → `npm run type-check` 零错误 → 关键路径实测 → 同步更新 docs

## 常用命令

```bash
dotnet build                                # 后端构建（0 警告 0 错误基线）
dotnet test                                 # 全部测试（37 单元 + 43 集成）
cd web && npm run type-check                # 前端类型检查
cd web && npm run dev                       # 启动前端（5173，代理 5026）
cd src/NetBase.Api && dotnet run            # 启动后端（5026）
```

## 提醒

- 集成测试连专用库 `NetBase_Test`（自动建库建表）；CI 中用 `NETBASE_TEST_CONNECTIONSTRING` 覆盖
- 新增/修改/删除功能、表、接口时，同步更新 `docs/技术文档.md` 变更记录
