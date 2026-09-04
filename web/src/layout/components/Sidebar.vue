<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { ElMenu } from 'element-plus'
import MenuTreeItem from './MenuTreeItem.vue'
import { usePermissionStore } from '@/stores/permission'

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
