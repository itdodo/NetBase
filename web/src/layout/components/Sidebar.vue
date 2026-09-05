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
    background-color="#1d2935"
    text-color="#a7b1c2"
    active-text-color="#ffffff"
    :collapse-transition="false"
    router
  >
    <MenuTreeItem v-for="menu in visibleMenus" :key="menu.id" :menu="menu" />
  </el-menu>
</template>

<style scoped>
.sidebar-menu {
  border-right: none;
}

.sidebar-menu :deep(.el-menu-item.is-active) {
  background-color: #409eff;
}
</style>
