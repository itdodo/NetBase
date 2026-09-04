<script setup lang="ts">
import { computed } from 'vue'
import type { MenuTree } from '@/types/api'
import { useUserStore } from '@/stores/user'
import { usePermissionStore } from '@/stores/permission'

const userStore = useUserStore()
const permissionStore = usePermissionStore()

const menuCount = computed(() => {
  let count = 0
  const walk = (list: MenuTree[]) =>
    list.forEach((m) => {
      count += 1
      walk(m.children)
    })
  walk(permissionStore.menus)
  return count
})
</script>

<template>
  <el-card>
    <template #header>欢迎使用 NetBase 管理系统</template>
    <el-descriptions :column="2" border>
      <el-descriptions-item label="当前用户">{{ userStore.userName }}</el-descriptions-item>
      <el-descriptions-item label="已加载菜单">{{ menuCount }} 项</el-descriptions-item>
      <el-descriptions-item label="后端框架">.NET 10 + SqlSugar + SqlServer</el-descriptions-item>
      <el-descriptions-item label="前端框架">Vue 3 + Element Plus + Vite</el-descriptions-item>
    </el-descriptions>
    <el-alert
      class="tip"
      type="warning"
      show-icon
      :closable="false"
      title="提示"
      description="菜单数据来自 /api/sys/menu/tree。若此处显示 0 项，请确认后端已启动且数据库初始化成功（appsettings.json 中 Db:InitEnabled=true）。"
    />
  </el-card>
</template>

<style scoped>
.tip {
  margin-top: 16px;
}
</style>
