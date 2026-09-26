import { ElMessage } from 'element-plus'
import type { Router } from 'vue-router'
import { ApiError } from '@/api/request'
import { usePermissionStore } from '@/stores/permission'
import { useUserStore } from '@/stores/user'

const WHITE_LIST = ['/login']

export function setupRouterGuard(router: Router): void {
  router.beforeEach(async (to) => {
    // 设置页面标题
    document.title = to.meta.title ? `${String(to.meta.title)} - NetBase` : 'NetBase 管理系统'

    const userStore = useUserStore()
    if (!userStore.token) {
      return WHITE_LIST.includes(to.path) ? true : { path: '/login', query: { redirect: to.fullPath } }
    }

    if (to.path === '/login') {
      return '/'
    }

    // 登录后首次导航：拉取菜单并注册动态路由，然后重放当前导航
    const permissionStore = usePermissionStore()
    if (!permissionStore.loaded) {
      try {
        const routes = await permissionStore.generateRoutes()
        // 无任何功能权限（未分配角色）：友好引导，而非降级报错
        if (routes.length === 0 && to.path !== '/403') {
          return '/403'
        }
        routes.forEach((route) => router.addRoute(route))
        return { path: to.path, query: to.query, replace: true }
      } catch (error) {
        // 401（登录过期/未登录）：forceLogout 已统一提示并跳转登录页，此处不再叠加提示
        if (error instanceof ApiError && error.code === 401) {
          return true
        }
        // 菜单加载失败（如后端未启动/数据库未初始化）：提示后降级进入，仅有静态路由
        ElMessage.error(`菜单加载失败，系统将以基础模式运行：${(error as Error).message}`)
        return true
      }
    }

    return true
  })
}
