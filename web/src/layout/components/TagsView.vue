<script setup lang="ts">
import { computed, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ArrowDown, Close } from '@element-plus/icons-vue'
import { useTabsStore } from '@/stores/tabs'

const route = useRoute()
const router = useRouter()
const tabsStore = useTabsStore()

// 路由变化时自动登记页签
watch(
  () => route.path,
  () => tabsStore.addView(route),
  { immediate: true },
)

const fixedPath = computed(() => tabsStore.visitedViews[0]?.path ?? '/dashboard')

function handleClick(path: string): void {
  if (path !== route.path) router.push(path)
}

function handleClose(path: string): void {
  const views = tabsStore.visitedViews
  const index = views.findIndex((v) => v.path === path)
  if (index === -1) return
  tabsStore.removeView(path)

  // 关闭的是当前页签时，跳到相邻页签（优先右侧，无则左侧）
  if (path === route.path) {
    const next = views[index + 1] ?? views[index - 1]
    if (next && next.path !== route.path) router.push(next.path)
  }
}

function handleCommand(command: string): void {
  if (command === 'closeOthers') {
    tabsStore.closeOthers(route.path)
  } else if (command === 'closeAll') {
    tabsStore.closeAll()
    if (route.path !== fixedPath.value) router.push(fixedPath.value)
  }
}
</script>

<template>
  <div class="tags-view">
    <div class="tags-scroll">
      <span
        v-for="view in tabsStore.visitedViews"
        :key="view.path"
        class="tag-item"
        :class="{ active: view.path === route.path }"
        @click="handleClick(view.path)"
      >
        {{ view.title }}
        <el-icon v-if="view.path !== fixedPath" class="tag-close" @click.stop="handleClose(view.path)">
          <Close />
        </el-icon>
      </span>
    </div>

    <el-dropdown trigger="click" @command="handleCommand">
      <el-icon class="tags-action" title="页签操作"><ArrowDown /></el-icon>
      <template #dropdown>
        <el-dropdown-menu>
          <el-dropdown-item command="closeOthers">关闭其他</el-dropdown-item>
          <el-dropdown-item command="closeAll">关闭所有</el-dropdown-item>
        </el-dropdown-menu>
      </template>
    </el-dropdown>
  </div>
</template>

<style scoped>
.tags-view {
  display: flex;
  align-items: center;
  height: 34px;
  padding: 0 8px;
  background: var(--el-bg-color);
  border-bottom: 1px solid var(--el-border-color-light);
}

.tags-scroll {
  flex: 1;
  display: flex;
  align-items: center;
  gap: 6px;
  overflow-x: auto;
  scrollbar-width: none;
}

.tags-scroll::-webkit-scrollbar {
  display: none;
}

.tag-item {
  display: inline-flex;
  align-items: center;
  gap: 2px;
  padding: 0 10px;
  height: 24px;
  font-size: 12px;
  color: var(--el-text-color-regular);
  background: var(--el-fill-color);
  border: 1px solid var(--el-border-color);
  border-radius: 3px;
  cursor: pointer;
  white-space: nowrap;
  user-select: none;
}

.tag-item:hover {
  color: #409eff;
}

.tag-item.active {
  color: #fff;
  background: #409eff;
  border-color: #409eff;
}

.tag-close {
  font-size: 12px;
  border-radius: 50%;
}

.tag-close:hover {
  color: #fff;
  background: rgba(0, 0, 0, 0.2);
}

.tags-action {
  margin-left: 8px;
  color: #909399;
  cursor: pointer;
}

.tags-action:hover {
  color: #409eff;
}
</style>
