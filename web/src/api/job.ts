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
