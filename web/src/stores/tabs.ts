import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { RouteLocationNormalized } from 'vue-router'

/** 页签数据 */
export interface TagView {
  path: string
  title: string
}

/** 首页固定页签，不可关闭 */
const AFFIX_VIEW: TagView = { path: '/dashboard', title: '首页' }

export const useTabsStore = defineStore('tabs', () => {
  const visitedViews = ref<TagView[]>([{ ...AFFIX_VIEW }])

  /** 路由进入时记录页签（404 不记录，重复不重复添加） */
  function addView(route: RouteLocationNormalized): void {
    if (!route.meta?.title || route.name === 'not-found') return
    if (visitedViews.value.some((v) => v.path === route.path)) return
    visitedViews.value.push({ path: route.path, title: String(route.meta.title) })
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
