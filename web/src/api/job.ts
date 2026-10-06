import request from './request'

export interface JobInstanceInfo {
  jobId: string
  displayName: string
  cron: string
  lastExecution?: string
  nextExecution?: string
  lastJobSuccess?: boolean
  paused: boolean
}

/** 作业实例列表 */
export const getJobList = () => request.get<never, JobInstanceInfo[]>('/monitor/job/list')

/** 修改 Cron（立即恢复调度） */
export const updateJobCron = (jobId: string, cron: string) =>
  request.put<never, void>(`/monitor/job/${jobId}/cron`, { cron })

/** 立即触发一次 */
export const triggerJob = (jobId: string) =>
  request.post<never, void>(`/monitor/job/${jobId}/trigger`)

/** 暂停作业 */
export const pauseJob = (jobId: string) => request.put<never, void>(`/monitor/job/${jobId}/pause`)

/** 恢复作业 */
export const resumeJob = (jobId: string) => request.put<never, void>(`/monitor/job/${jobId}/resume`)
import type { PageQuery, PageResult } from '@/types/api'

export interface JobLogInfo {
  id: string
  jobId: string
  jobName: string
  success: boolean
  durationMs: number
  error?: string
  /** scheduled=按 Cron 调度 / manual=手动触发 */
  triggerType: string
  executedAt: string
}

/** 作业执行日志分页（jobId/success 筛选） */
export const getJobLogs = (params: Partial<PageQuery> & { jobId?: string; success?: boolean }) =>
  request.get<never, PageResult<JobLogInfo>>('/monitor/job/logs', { params })
