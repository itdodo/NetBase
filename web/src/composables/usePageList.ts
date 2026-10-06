/**
 * 列表页通用组合式函数：封装 loading/列表/分页/查询/重置 样板。
 * Q 为查询条件类型（含 pageIndex/pageSize），T 为行类型。
 * 迁移示例见 views/monitor/operlog、views/monitor/loginlog。
 */
import { computed, onScopeDispose, reactive, ref, type Ref } from 'vue'
import request, { CONCURRENCY_CONFLICT_EVENT } from '@/api/request'

export interface UsePageListOptions<T, Q> {
  /** 接口地址（GET 分页） */
  url: string
  /** 初始查询条件（必须包含完整的查询字段，决定 query 的类型） */
  defaultQuery: Q
  /** 加载成功后的回调（可选） */
  onLoaded?: (items: T[], total: number) => void
}

export function usePageList<T = unknown, Q extends { pageIndex: number; pageSize: number } = { pageIndex: number; pageSize: number }>(
  options: UsePageListOptions<T, Q>
) {
  const { url } = options
  const loading = ref(false)
  const list = ref<T[]>([]) as Ref<T[]>
  const total = ref(0)

  // 首次加载（列表尚无数据）：页面据此显示骨架屏而非 v-loading 遮罩
  const firstLoading = computed(() => loading.value && list.value.length === 0)

  const query = reactive({ ...options.defaultQuery }) as { pageIndex: number; pageSize: number } & Q

  async function loadData(): Promise<void> {
    loading.value = true
    try {
      const page = await request.get<never, { items: T[]; total: number }>(url, { params: query })
      list.value = page.items
      total.value = page.total
      options.onLoaded?.(page.items, page.total)
    } finally {
      loading.value = false
    }
  }

  function handleSearch(): void {
    query.pageIndex = 1
    loadData()
  }

  /** 重置到初始查询条件后重新加载（pageIndex 回到 1） */
  function handleReset(): void {
    Object.assign(query, options.defaultQuery, { pageIndex: 1 })
    loadData()
  }

  // 乐观锁并发冲突：任意接口返回 COMMON_CONCURRENCY_CONFLICT 时刷新本列表（拿到他人修改后的最新版本）
  const onConcurrencyConflict = (): void => {
    void loadData()
  }
  window.addEventListener(CONCURRENCY_CONFLICT_EVENT, onConcurrencyConflict as EventListener)
  onScopeDispose(() => window.removeEventListener(CONCURRENCY_CONFLICT_EVENT, onConcurrencyConflict as EventListener))

  return { loading, firstLoading, list, total, query, loadData, handleSearch, handleReset }
}
