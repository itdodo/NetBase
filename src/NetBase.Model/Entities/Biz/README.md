# 业务模块目录

本目录存放**业务代码**，与框架层（Sys 前缀 / System|Monitor 控制器）物理隔离。

开发前必读：[docs/业务开发规范.md](../../../docs/业务开发规范.md)

要点：
- 实体表名 `biz_` 前缀；需要数据隔离时实现 `IDataScope`（DeptId/OwnerUserId 真实列）
- 服务继承 `BaseService<T>`（GetRequired/分页/事务已就绪）
- 控制器继承 `BaseController`，路由 `api/v1/biz/{module}`，写操作打 `[HasPermission]` + `[NoRepeatSubmit]`
- 权限码 `biz:{模块}:{动作}`，业务菜单种子写入业务分支自建的 `BizSeeder`
