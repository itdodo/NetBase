import axios from 'axios'
import { ElMessage } from 'element-plus'
import { clearToken, getRefreshToken, getToken, setRefreshToken, setToken } from '@/utils/auth'
import { refreshTokenApi } from '@/api/auth'

/**
 * axios 实例：响应拦截器统一解包 ApiResult.data，
 * 非 200 业务码 / HTTP 错误统一提示并 reject；
 * 401 时用 RefreshToken 静默续期（单飞：并发 401 只刷新一次）并重放原请求。
 */
const request = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
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

/** 刷新失败统一登出跳转 */
function forceLogout(): void {
  clearToken()
  if (location.pathname !== '/login') {
    const redirect = encodeURIComponent(location.pathname + location.search)
    location.href = `/login?redirect=${redirect}`
  }
}

request.interceptors.response.use(
  (response) => {
    const result = response.data as { code: number; message: string; data: unknown }
    if (result.code !== 200) {
      ElMessage.error(result.message || '操作失败')
      return Promise.reject(new Error(result.message))
    }
    // 直接返回业务数据，调用方拿到的就是 data 字段
    return result.data as never
  },
  async (error) => {
    const config = error.config ?? {}
    const status = error.response?.status
    const message = error.response?.data?.message

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
      return Promise.reject(error)
    }

    if (status === 401) {
      forceLogout()
      return Promise.reject(error)
    }

    ElMessage.error(message || error.message || '网络异常，请稍后重试')
    return Promise.reject(error)
  }
)

export default request
