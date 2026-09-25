-- 0001_baseline：迁移管线基线标记（幂等空操作）
-- 用途：为存量库建立迁移起点。此后所有结构性/数据性变更请新增 NNNN_描述.sql，
-- 历史脚本禁止修改；脚本必须幂等。
-- 注：2026-09-25 数据库引擎由 SqlServer 切换为 PostgreSQL，本脚本随方言整体移植（特例）。
SELECT CASE WHEN to_regclass('public.sys_db_migration') IS NOT NULL
            THEN 1 ELSE 0 END AS baselinereached;
