-- 0001_baseline：迁移管线基线标记（幂等空操作）
-- 用途：为存量库建立迁移起点。此后所有结构性/数据性变更请新增 NNNN_描述.sql，
-- 历史脚本禁止修改；脚本必须幂等（IF NOT EXISTS / IF EXISTS 守卫）。
IF OBJECT_ID(N'dbo.sys_db_migration') IS NOT NULL
    SELECT 1 AS BaselineReached
ELSE
    SELECT 0 AS BaselineReached;
