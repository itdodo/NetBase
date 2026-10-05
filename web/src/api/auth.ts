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

/** 忘记密码：发送重置验证码到账号预留邮箱（后端防枚举，统一提示） */
export const forgotPassword = (data: { userName: string; email: string }) =>
  request.post<never, string>('/auth/forgot-password', data)

/** 忘记密码：凭邮箱验证码重置密码（成功后全端下线） */
export const resetPasswordByCode = (data: { userName: string; email: string; code: string; newPassword: string }) =>
  request.post<never, string>('/auth/reset-password', data)

/** 获取图形验证码（返回 SVG 字符串） */
export const getCaptcha = () =>
  request.get<never, { captchaId: string; svg: string }>('/auth/captcha')

/** 验证码开关（登录页据此决定是否展示验证码输入） */
export const getCaptchaEnabled = () => request.get<never, boolean>('/auth/captcha/enabled')

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

/** 刷新令牌（轮换）。失败由 request.ts 的 forceLogout 统一提示，请求标记 _silentAuth 全程静默 */
export const refreshTokenApi = (refreshToken: string) =>
  request.post<never, LoginResponse>('/auth/refresh', { refreshToken }, { _silentAuth: true })

/** 登出 */
export const logoutApi = () => request.post<never, void>('/auth/logout')

/** 当前用户信息 + 权限码 */
export const getProfile = () => request.get<never, { user: LoginResponse['user']; permissions: string[] }>('/auth/profile')

/** 在线会话分页 */
export const getSessionPage = (params: Partial<PageQuery>) =>
  request.get<never, PageResult<SessionInfo>>('/auth/sessions', { params })

/** 强制下线 */
export const kickSession = (id: number) => request.delete<never, void>(`/auth/sessions/${id}`)
