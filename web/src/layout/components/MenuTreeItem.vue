<script setup lang="ts">
import { computed } from 'vue'
import type { MenuTree } from '@/types/api'
import { resolveMenuIcon } from '@/utils/menuIcon'

defineOptions({ name: 'MenuTreeItem' })

const props = defineProps<{ menu: MenuTree }>()

const icon = computed(() => resolveMenuIcon(props.menu.icon))

/** 仅展示可见且启用的目录/菜单（按钮类型不上侧边栏） */
const children = computed(() =>
  props.menu.children
    .filter((m) => m.visible && m.status === 1 && m.menuType !== 3)
    .sort((a, b) => a.sort - b.sort)
)

const isDirectory = computed(() => props.menu.menuType === 1 && children.value.length > 0)
</script>

<template>
  <el-sub-menu v-if="isDirectory" :index="menu.path || `menu-${menu.id}`">
    <template #title>
      <el-icon v-if="icon">
        <component :is="icon" />
      </el-icon>
      <span>{{ menu.menuName }}</span>
    </template>
    <MenuTreeItem v-for="child in children" :key="child.id" :menu="child" />
  </el-sub-menu>

  <el-menu-item v-else :index="menu.path || `menu-${menu.id}`">
    <el-icon v-if="icon">
      <component :is="icon" />
    </el-icon>
    <template #title>{{ menu.menuName }}</template>
  </el-menu-item>
</template>
