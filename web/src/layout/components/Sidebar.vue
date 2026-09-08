<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import MenuTreeItem from './MenuTreeItem.vue'
import { usePermissionStore } from '@/stores/permission'

// 注意：el-menu/el-sub-menu/el-menu-item 由 unplugin-vue-components 按需导入（含样式），
// 此处不可显式 import 组件，否则绕过 resolver 导致样式缺失（菜单布局错乱的根因）。

defineProps<{ collapse: boolean }>()

const route = useRoute()
const permissionStore = usePermissionStore()

const visibleMenus = computed(() =>
  permissionStore.menus
    .filter((m) => m.parentId === 0 && m.visible && m.status === 1 && m.menuType !== 3)
    .sort((a, b) => a.sort - b.sort)
)

const activePath = computed(() => route.path)
</script>

<template>
  <el-menu
    class="sidebar-menu"
    :collapse="collapse"
    :default-active="activePath"
    :collapse-transition="false"
    unique-opened
    router
  >
    <MenuTreeItem v-for="menu in visibleMenus" :key="menu.id" :menu="menu" />
  </el-menu>
</template>

<style scoped>
.sidebar-menu {
  border-right: none;
  /* 颜色来自 layout .aside 的主题变量，亮暗自动切换 */
  --el-menu-bg-color: var(--sidebar-bg, #1d2935);
  --el-menu-text-color: var(--sidebar-text, #a7b1c2);
  --el-menu-hover-bg-color: var(--sidebar-hover-bg, rgba(255, 255, 255, 0.08));
  --el-menu-active-color: #ffffff;
}

.sidebar-menu :deep(.el-menu-item.is-active) {
  background-color: #409eff;
  color: #fff;
}

.sidebar-menu :deep(.el-menu-item:hover),
.sidebar-menu :deep(.el-sub-menu__title:hover) {
  background-color: var(--sidebar-hover-bg, rgba(255, 255, 255, 0.08));
}
</style>
