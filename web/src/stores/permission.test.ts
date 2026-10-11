import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { usePermissionStore } from '@/stores/permission'
import type { MenuTree } from '@/types/api'

/**
 * 权限判定核心：v-permission 指令、菜单过滤、接口守卫全部依赖 permissions 集合。
 * permissions 是 menus 的只读 computed——测试通过驱动真实输入（菜单树）验证推导语义。
 */
function menu(permission: string, children: MenuTree[] = []): MenuTree {
  return { id: permission, parentId: '0', menuName: permission, menuType: 3, permission, children } as unknown as MenuTree
}

describe('permission store 权限推导', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('从菜单树推导权限码（含子级递归）', () => {
    const store = usePermissionStore()
    store.menus = [menu('sys:user:dir', [menu('sys:user:list'), menu('sys:user:add')]), menu('monitor:online:list')]
    expect(store.permissions.has('sys:user:list')).toBe(true)
    expect(store.permissions.has('sys:user:add')).toBe(true)
    expect(store.permissions.has('monitor:online:list')).toBe(true)
    expect(store.permissions.has('sys:menu:list')).toBe(false)
  })

  it('v-permission 语义：多码数组任一满足（store.permissions.has 组合）', () => {
    const store = usePermissionStore()
    store.menus = [menu('sys:user:list')]
    const required = ['sys:user:add', 'sys:user:list']
    expect(required.some((code) => store.permissions.has(code))).toBe(true)
    expect(['sys:user:edit'].some((code) => store.permissions.has(code))).toBe(false)
  })

  it('reset 后菜单与加载态清空（退出登录语义）', () => {
    const store = usePermissionStore()
    store.menus = [menu('sys:user:list')]
    store.loaded = true
    store.reset()
    expect(store.menus.length).toBe(0)
    expect(store.permissions.size).toBe(0)
    expect(store.loaded).toBe(false)
  })
})
