import request from './request'

export interface SystemMonitorInfo {
  machineName: string
  processArchitecture: string
  uptimeHours: number
  workingSetMb: number
  gcMemoryMb: number
  threadCount: number
  handleCount: number
  gen0Collections: number
  gen1Collections: number
  gen2Collections: number
  cacheProvider: string
  cacheDiagnostics: Record<string, unknown> | null
}

export const getSystemMonitor = () => request.get<never, SystemMonitorInfo>('/monitor/system')

/** 清理操作日志（物理删除，保留 keepDays 天） */
export const cleanupOperationLogs = (keepDays: number) =>
  request.delete<never, number>('/sys/log/operation/cleanup', { params: { keepDays } })

/** 清理登录日志 */
export const cleanupLoginLogs = (keepDays: number) =>
  request.delete<never, number>('/sys/log/login/cleanup', { params: { keepDays } })
