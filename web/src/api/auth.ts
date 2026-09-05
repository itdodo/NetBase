import request from './request'
import type { PageQuery, PageResult, RoleSimple } from '@/types/api'

export interface LoginResponse {
  accessToken: string
  refreshToken: string
  expiresIn: number
  user: {
    id: number
    userName: string
    nickName?: string
    roles: RoleSimple[]
  }
  permissions: string[]
}

export interface SessionInfo {
  id: number
  userId: number
  userName: string
  nickName?: string
  loginIp?: string
  userAgent?: string
  loginTime: string
  expireTime: string
}

/** 登录 */
export const login = (data: { userName: string; password: string }) =>
  request.post<never, LoginResponse>('/auth/login', data)

/** 刷新令牌（轮换） */
export const refreshTokenApi = (refreshToken: string) =>
  request.post<never, LoginResponse>('/auth/refresh', { refreshToken })

/** 登出 */
export const logoutApi = () => request.post<never, void>('/auth/logout')

/** 当前用户信息 + 权限码 */
export const getProfile = () => request.get<never, { user: LoginResponse['user']; permissions: string[] }>('/auth/profile')

/** 在线会话分页 */
export const getSessionPage = (params: Partial<PageQuery>) =>
  request.get<never, PageResult<SessionInfo>>('/auth/sessions', { params })

/** 强制下线 */
export const kickSession = (id: number) => request.delete<never, void>(`/auth/sessions/${id}`)
