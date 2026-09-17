import request from './request'
import type { PageQuery, PageResult, User } from '@/types/api'

export interface LoginResponse {
  accessToken: string
  refreshToken: string
  expiresIn: number
  user: User
  permissions: string[]
  /** 密码已超期（sys.pwd.expireDays 启用时），登录后须强制修改 */
  mustChangePassword?: boolean
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

export interface LoginPayload {
  userName: string
  password: string
  captchaId?: string
  captchaCode?: string
}

/** 获取图形验证码（返回 SVG 字符串） */
export const getCaptcha = () =>
  request.get<never, { captchaId: string; svg: string }>('/auth/captcha')

/** 登录 */
export const login = (data: LoginPayload) =>
  request.post<never, LoginResponse>('/auth/login', data)

/** 修改自己资料 */
export const updateProfile = (data: { nickName?: string; phone?: string; email?: string }) =>
  request.put<never, void>('/auth/profile', data)

/** 上传头像（multipart），返回访问地址 */
export const uploadAvatar = (file: File) => {
  const form = new FormData()
  form.append('file', file)
  form.append('bizType', 'avatar')
  return request.post<never, string>('/auth/avatar', form)
}

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
