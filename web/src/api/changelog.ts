import request from './request'
import type { PageQuery, PageResult } from '@/types/api'

/** 字段级变更日志行（sys_change_log） */
export interface ChangeLogInfo {
  /** 雪花ID（后端已字符串化防精度丢失） */
  id: string
  /** 业务表名（实体名，如 SysRole） */
  tableName: string
  /** 数据主键 */
  recordId: string
  /** 变更明细 JSON：{ "字段": { "old": 旧值, "new": 新值 } }（敏感字段已脱敏） */
  changes: string
  /** 操作人ID（字符串化） */
  userId: string
  userName?: string
  createTime: string
}

export interface ChangeLogQuery extends PageQuery {
  tableName?: string
  userName?: string
  beginTime?: string
  endTime?: string
}

/** 变更日志分页 */
export const getChangeLogPage = (params: Partial<ChangeLogQuery>) =>
  request.get<never, PageResult<ChangeLogInfo>>('/sys/changelog/page', { params })

/** 清理变更日志（物理删除 keepDays 天前记录） */
export const cleanupChangeLogs = (keepDays: number) =>
  request.delete<never, number>('/sys/changelog/cleanup', { params: { keepDays } })
