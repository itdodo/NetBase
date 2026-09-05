import axios from 'axios'
import { ElMessage } from 'element-plus'
import { clearToken, getToken } from '@/utils/auth'

/**
 * axios 实例：响应拦截器统一解包 ApiResult.data，
 * 非 200 业务码 / HTTP 错误统一提示并 reject。
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
  (error) => {
    const status = error.response?.status
    const message = error.response?.data?.message
    if (status === 401) {
      clearToken()
      // 认证接入后此处跳登录页并携带回跳地址；避免在登录页循环提示
      if (location.pathname !== '/login') {
        const redirect = encodeURIComponent(location.pathname + location.search)
        location.href = `/login?redirect=${redirect}`
      }
      return Promise.reject(error)
    }
    ElMessage.error(message || error.message || '网络异常，请稍后重试')
    return Promise.reject(error)
  }
)

export default request
