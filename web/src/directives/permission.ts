import type { App, Directive, DirectiveBinding } from 'vue'
import { usePermissionStore } from '@/stores/permission'

/**
 * 按钮级权限指令：无对应权限码时直接移除元素。
 * 用法：v-permission="'sys:user:add'" 或 v-permission="['sys:user:add', 'sys:user:edit']"（任一满足即可）
 */
const permission: Directive<HTMLElement, string | string[]> = {
  mounted(el: HTMLElement, binding: DirectiveBinding<string | string[]>) {
    const store = usePermissionStore()
    const required = Array.isArray(binding.value) ? binding.value : [binding.value]
    const allowed = required.some((code) => store.permissions.has(code))
    if (!allowed) {
      el.parentNode?.removeChild(el)
    }
  }
}

export function setupPermissionDirective(app: App): void {
  app.directive('permission', permission)
}
