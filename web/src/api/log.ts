import request from './request'
import type { PageQuery, PageResult } from '@/types/api'

export interface OperationLogInfo {
  id: number
  userId: number
  userName?: string
  module?: string
  action?: string
  httpMethod?: string
  path?: string
  params?: string
  success: boolean
  errorMessage?: string
  elapsedMs: number
  ip?: string
  createTime: string
}

export interface LoginLogInfo {
  id: number
  userId: number
  userName: string
  success: boolean
  message?: string
  ip?: string
  userAgent?: string
  createTime: string
}

export interface LogQuery extends PageQuery {
  keyword?: string
  success?: number
}

/** 操作日志分页 */
export const getOperationLogPage = (params: Partial<LogQuery>) =>
  request.get<never, PageResult<OperationLogInfo>>('/sys/log/operation/page', { params })

/** 登录日志分页 */
export const getLoginLogPage = (params: Partial<LogQuery>) =>
  request.get<never, PageResult<LoginLogInfo>>('/sys/log/login/page', { params })

/** 修改自己密码（成功后需重新登录） */
export const changePassword = (data: { oldPassword: string; newPassword: string }) =>
  request.post<never, void>('/auth/change-password', data)
