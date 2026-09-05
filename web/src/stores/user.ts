import { defineStore } from 'pinia'
import { ref } from 'vue'
import { login as apiLogin, logoutApi } from '@/api/auth'
import { clearToken, getToken, getUserName, setRefreshToken, setToken, setUserName } from '@/utils/auth'

/**
 * 用户状态：JWT 登录（accessToken + refreshToken）。
 * 权限码与菜单由 permission store 从菜单树获取（按钮即菜单，口径一致）。
 */
export const useUserStore = defineStore('user', () => {
  const token = ref<string>(getToken() || '')
  const userName = ref<string>(getUserName())

  async function login(form: { userName: string; password: string }): Promise<void> {
    const res = await apiLogin(form)
    token.value = res.accessToken
    userName.value = res.user.userName
    setToken(res.accessToken)
    setRefreshToken(res.refreshToken)
    setUserName(res.user.userName)
  }

  async function logout(): Promise<void> {
    try {
      await logoutApi()
    } catch {
      // 服务端会话清理失败不阻断本地登出
    } finally {
      token.value = ''
      userName.value = ''
      clearToken()
    }
  }

  return { token, userName, login, logout }
})
