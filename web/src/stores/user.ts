import { defineStore } from 'pinia'
import { ref } from 'vue'
import { clearToken, getToken, getUserName, setToken, setUserName } from '@/utils/auth'

/**
 * 用户状态：当前认证未接入（后端 JWT 待定），login 为本地模拟，
 * 接入后仅需把 login 内部替换为 POST /api/auth/login 调用即可，页面无需改动。
 */
export const useUserStore = defineStore('user', () => {
  const token = ref<string>(getToken() || '')
  const userName = ref<string>(getUserName())

  async function login(form: { userName: string; password: string }): Promise<void> {
    // TODO: 接入认证后替换为真实登录接口，由后端签发 JWT
    // const res = await request.post<never, { token: string }>('/auth/login', form)
    if (!form.userName || !form.password) {
      throw new Error('请输入账号和密码')
    }
    token.value = `mock-token-${Date.now()}`
    userName.value = form.userName
    setToken(token.value)
    setUserName(userName.value)
  }

  function logout(): void {
    token.value = ''
    userName.value = ''
    clearToken()
  }

  return { token, userName, login, logout }
})
