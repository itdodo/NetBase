<script setup lang="ts">
import { computed } from 'vue'
import type { Component } from 'vue'
import * as ElementIcons from '@element-plus/icons-vue'
import type { MenuTree } from '@/types/api'

defineOptions({ name: 'MenuTreeItem' })

const props = defineProps<{ menu: MenuTree }>()

/**
 * 菜单 icon 字段动态匹配 Element Plus 图标（支持 kebab/小写，如 setting → Setting），
 * 菜单管理里可直接填写任意 Element Plus 图标名，无需改前端代码。
 */
const icon = computed<Component | undefined>(() => {
  const key = props.menu.icon
  if (!key) return undefined
  const pascal = key
    .replace(/(^|[-_])([a-z])/g, (_, __, c: string) => c.toUpperCase())
    .replace(/^./, (c) => c.toUpperCase())
  return (ElementIcons as Record<string, Component>)[pascal]
})

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
