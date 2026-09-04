<script setup lang="ts">
import { computed } from 'vue'
import type { Component } from 'vue'
import { Coin, Menu as MenuIcon, Monitor, Setting, Tools } from '@element-plus/icons-vue'
import type { MenuTree } from '@/types/api'

defineOptions({ name: 'MenuTreeItem' })

const props = defineProps<{ menu: MenuTree }>()

/** 菜单 icon 字符串到 Element 图标的映射，未匹配则不显示图标 */
const iconMap: Record<string, Component> = {
  setting: Setting,
  monitor: Monitor,
  system: Setting,
  menu: MenuIcon,
  user: Coin,
  tool: Tools
}

const icon = computed(() => (props.menu.icon ? iconMap[props.menu.icon] : undefined))

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
