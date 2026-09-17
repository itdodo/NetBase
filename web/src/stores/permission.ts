import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import type { RouteRecordRaw } from 'vue-router'
import Layout from '@/layout/index.vue'
import { getMyMenuTree } from '@/api/menu'
import type { MenuTree } from '@/types/api'

/** views 目录映射：菜单的 component 字段（如 system/user/index）到页面组件 */
const viewModules = import.meta.glob('../views/**/*.vue')

function loadView(component: string) {
  const loader = viewModules[`../views/${component}.vue`]
  // 找不到组件时回退到 404，避免路由渲染报错
  return loader ?? viewModules['../views/error/404.vue']
}

/**
 * 权限状态：负责拉取菜单树、收集权限码、把"菜单"类型节点注册为动态路由。
 * 路由形态：每个菜单页面包一层 Layout，path 使用菜单完整路径（如 /system/user）。
 */
export const usePermissionStore = defineStore('permission', () => {
  const menus = ref<MenuTree[]>([])
  const loaded = ref(false)

  const permissions = computed(() => {
    const codes = new Set<string>()
    const walk = (list: MenuTree[]) =>
      list.forEach((m) => {
        if (m.permission) codes.add(m.permission)
        walk(m.children)
      })
    walk(menus.value)
    return codes
  })

  /** 拉取菜单并生成动态路由；菜单接口失败时降级（仅保留静态路由），不阻断进入系统 */
  async function generateRoutes(): Promise<RouteRecordRaw[]> {
    try {
      menus.value = await getMyMenuTree()
    } finally {
      loaded.value = true
    }

    const routes: RouteRecordRaw[] = []
    const collect = (list: MenuTree[]) =>
      list.forEach((m) => {
        // 类型 2-菜单 注册为页面路由；目录仅用于侧边栏分组；按钮无页面
        if (m.menuType === 2) {
          const component = m.component || ''
          // 缓存名与页面组件 defineOptions name 约定：system/user/index → SystemUserView
          // （index 段为目录约定不参与命名；非 index 结尾如 system/user/list → SystemUserListView）
          const cachedName = component
            ? component
                .split('/')
                .filter((seg) => seg.toLowerCase() !== 'index')
                .map((seg) => seg.charAt(0).toUpperCase() + seg.slice(1))
                .join('') + 'View'
            : undefined
          routes.push({
            path: m.path || `/menu-${m.id}`,
            name: `menu-${m.id}`,
            component: Layout,
            children: [
              {
                path: '',
                name: `menu-${m.id}-index`,
                component: m.component ? loadView(m.component) : loadView('error/404'),
                meta: { title: m.menuName, icon: m.icon, cachedName }
              }
            ]
          })
        }
        collect(m.children)
      })
    collect(menus.value)
    return routes
  }

  function reset(): void {
    menus.value = []
    loaded.value = false
  }

  return { menus, loaded, permissions, generateRoutes, reset }
})
