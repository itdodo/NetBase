import axios from 'axios'
import { ElMessage } from 'element-plus'
import { clearToken, getRefreshToken, getToken, setRefreshToken, setToken } from '@/utils/auth'
import { refreshTokenApi } from '@/api/auth'
import type { ApiResult } from '@/types/api'
import { ERROR_CODES } from '@/types/errorCodes'

/** 乐观锁并发冲突事件名：request.ts 在收到 COMMON_CONCURRENCY_CONFLICT 时派发，usePageList 监听自动刷新 */
export const CONCURRENCY_CONFLICT_EVENT = 'nb:concurrency-conflict'

/** 业务错误：携带统一返回的 code 与全局 errorCode，页面可按码分支（码表 types/errorCodes.ts） */
export class ApiError extends Error {
  /** 统一返回码（200 成功，400/401/403/404/409/500 见后端 ApiResultCode） */
  code: number
  /** 全局业务错误码（如 SYS_USER_NOT_FOUND） */
  errorCode?: string

  constructor(message: string, code: number, errorCode?: string) {
    super(message)
    this.name = 'ApiError'
    this.code = code
    this.errorCode = errorCode
  }
}

declare module 'axios' {
  export interface AxiosRequestConfig {
    /** 指定这些 errorCode 由调用方自行处理，拦截器不再弹全局错误提示 */
    silentErrorCodes?: string[]
    /** 401 刷新重放标记（内部使用） */
    _retried?: boolean
  }
}

/**
 * axios 实例：响应拦截器统一解包 ApiResult.data，
 * 非 200 业务码 / HTTP 错误统一提示并 reject ApiError（code 不再丢失）；
 * 401 时用 RefreshToken 静默续期（单飞：并发 401 只刷新一次）并重放原请求。
 */
const request = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api/v1',
  timeout: 15000
})

request.interceptors.request.use((config) => {
  const token = getToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

/** 是否正在刷新（单飞） */
let refreshing: Promise<boolean> | null = null

async function doRefresh(): Promise<boolean> {
  const token = getRefreshToken()
  if (!token) return false
  try {
    const res = await refreshTokenApi(token)
    setToken(res.accessToken)
    setRefreshToken(res.refreshToken)
    return true
  } catch {
    return false
  }
}

/** 刷新失败统一登出：友好提示 + 2000ms 自动跳登录页（并发 401 仅提示一次） */
let forceLogoutTimer: ReturnType<typeof setTimeout> | null = null

function forceLogout(reason?: string): void {
  clearToken()
  if (forceLogoutTimer !== null || location.pathname === '/login') {
    return
  }
  ElMessage.warning(reason || '您的登录已失效，即将返回登录页')
  forceLogoutTimer = setTimeout(() => {
    forceLogoutTimer = null
    const redirect = encodeURIComponent(location.pathname + location.search)
    location.href = `/login?redirect=${redirect}`
  }, 2000)
}

/** silentErrorCodes 命中时跳过全局提示（调用方按码自行处理） */
function isSilent(config: { silentErrorCodes?: string[] }, errorCode?: string): boolean {
  return errorCode !== undefined && !!config.silentErrorCodes?.includes(errorCode)
}

request.interceptors.response.use(
  async (response) => {
    // 二进制响应（文件导出）：正常直接透传；报错时 content-type 为 json，
    // 还原为统一错误提示（避免把错误 JSON 当文件下载成坏文件）
    if (response.config.responseType === 'blob') {
      const contentType = String(response.headers['content-type'] ?? '')
      if (contentType.includes('application/json')) {
        const text = await (response.data as Blob).text()
        let result: ApiResult
        try {
          result = JSON.parse(text) as ApiResult
        } catch {
          ElMessage.error('导出失败，请稍后重试')
          return Promise.reject(new ApiError('导出失败', 500))
        }
        if (result.code !== 200) {
          if (result.errorCode === ERROR_CODES.COMMON_CONCURRENCY_CONFLICT) {
            window.dispatchEvent(new CustomEvent(CONCURRENCY_CONFLICT_EVENT))
          }
          if (!isSilent(response.config, result.errorCode)) {
            ElMessage.error(result.message || '导出失败')
          }
          return Promise.reject(new ApiError(result.message || '导出失败', result.code, result.errorCode))
        }
      }
      return response.data as never
    }
    const result = response.data as ApiResult
    if (result.code !== 200) {
      // 乐观锁并发冲突：广播事件，挂载中的列表页自动刷新到最新版本
      if (result.errorCode === ERROR_CODES.COMMON_CONCURRENCY_CONFLICT) {
        window.dispatchEvent(new CustomEvent(CONCURRENCY_CONFLICT_EVENT))
      }
      if (!isSilent(response.config, result.errorCode)) {
        ElMessage.error(result.message || '操作失败')
      }
      return Promise.reject(new ApiError(result.message || '操作失败', result.code, result.errorCode))
    }
    // 直接返回业务数据，调用方拿到的就是 data 字段
    return result.data as never
  },
  async (error) => {
    const config = error.config ?? {}
    const status: number | undefined = error.response?.status
    const body = (error.response?.data ?? {}) as Partial<ApiResult>
    const message = body.message

    // 401：排除登录/刷新接口本身，尝试静默续期并重放一次
    if (status === 401 && !config._retried && !config.url?.includes('/auth/login') && !config.url?.includes('/auth/refresh')) {
      refreshing ??= doRefresh().finally(() => {
        refreshing = null
      })
      const ok = await refreshing
      if (ok) {
        config._retried = true
        config.headers = { ...config.headers, Authorization: `Bearer ${getToken()}` }
        return request(config)
      }
      forceLogout()
      return Promise.reject(new ApiError(message || '登录已失效', 401, body.errorCode))
    }

    if (status === 401) {
      forceLogout()
      return Promise.reject(new ApiError(message || '登录已失效', 401, body.errorCode))
    }

    // 403：授权失败（响应体带统一结构，优先读后端文案）
    if (status === 403) {
      ElMessage.error(message || '没有操作权限，请联系管理员')
      return Promise.reject(new ApiError(message || '没有操作权限', 403, body.errorCode))
    }

    // 429：限流（登录尝试过于频繁）
    if (status === 429) {
      ElMessage.error('操作过于频繁，请稍后再试')
      return Promise.reject(new ApiError('操作过于频繁', 429, body.errorCode))
    }

    // 5xx/网络层失败（后端不可达、代理断连）：与业务错误区分提示
    if (!error.response || (status !== undefined && status >= 500)) {
      ElMessage.error('服务暂时不可用，请稍后重试；若持续失败请联系管理员')
      return Promise.reject(new ApiError('服务暂时不可用', status ?? 0, body.errorCode))
    }

    if (!isSilent(config, body.errorCode)) {
      ElMessage.error(message || error.message || '网络异常，请稍后重试')
    }
    return Promise.reject(new ApiError(message || error.message || '网络异常', status ?? 0, body.errorCode))
  }
)

export default request
