import { defineStore } from 'pinia'
import { ref, watch } from 'vue'
import type { RouteLocationNormalized } from 'vue-router'

/** 页签数据（cachedName 用于 keep-alive 缓存匹配） */
export interface TagView {
  path: string
  title: string
  cachedName?: string
}

/** 首页固定页签，不可关闭 */
const AFFIX_VIEW: TagView = { path: '/dashboard', title: '首页', cachedName: 'DashboardView' }

const STORAGE_KEY = 'netbase:tabs'

/** 从 sessionStorage 恢复页签（结构无效时回退固定页签） */
function loadViews(): TagView[] {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (raw) {
      const arr = JSON.parse(raw) as TagView[]
      if (Array.isArray(arr) && arr.length > 0 && arr[0].path === AFFIX_VIEW.path) {
        return arr
      }
    }
  } catch {
    // 解析失败按无缓存处理
  }
  return [{ ...AFFIX_VIEW }]
}

export const useTabsStore = defineStore('tabs', () => {
  const visitedViews = ref<TagView[]>(loadViews())

  // 页签变化持久化到 sessionStorage（刷新不丢，关闭标签页即清除）
  watch(
    visitedViews,
    (views) => sessionStorage.setItem(STORAGE_KEY, JSON.stringify(views)),
    { deep: true },
  )

  /** 路由进入时记录页签（404 不记录，重复时补齐缓存名以兼容历史持久化数据） */
  function addView(route: RouteLocationNormalized): void {
    if (!route.meta?.title || route.name === 'not-found') return
    const cachedName = route.meta.cachedName as string | undefined
    const existing = visitedViews.value.find((v) => v.path === route.path)
    if (existing) {
      // 缓存名随路由 meta 更新（兼容持久化的历史数据 / 菜单组件路径变更）
      if (existing.cachedName !== cachedName) existing.cachedName = cachedName
      return
    }
    visitedViews.value.push({
      path: route.path,
      title: String(route.meta.title),
      cachedName,
    })
  }

  function removeView(path: string): void {
    if (path === AFFIX_VIEW.path) return
    visitedViews.value = visitedViews.value.filter((v) => v.path !== path)
  }

  /** 关闭其他，仅保留固定页签与指定页签 */
  function closeOthers(path: string): void {
    visitedViews.value = visitedViews.value.filter(
      (v) => v.path === AFFIX_VIEW.path || v.path === path,
    )
  }

  /** 关闭所有，仅保留固定页签 */
  function closeAll(): void {
    visitedViews.value = [{ ...AFFIX_VIEW }]
  }

  return { visitedViews, addView, removeView, closeOthers, closeAll }
})
