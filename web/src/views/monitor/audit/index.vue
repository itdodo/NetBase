<script setup lang="ts">
import { computed, ref } from 'vue'
import { usePermissionStore } from '@/stores/permission'
import OperLogTab from './OperLogTab.vue'
import LoginLogTab from './LoginLogTab.vue'
import ChangeLogTab from './ChangeLogTab.vue'

defineOptions({ name: 'MonitorAuditView' })

/** 审计日志聚合页：三类日志按权限显隐页签（页签内容懒加载，切走不丢状态） */
const permStore = usePermissionStore()
const activeTab = ref('operlog')

const tabs = computed(() =>
  [
    { name: 'operlog', label: '操作日志', perm: 'monitor:operlog:list', component: OperLogTab },
    { name: 'loginlog', label: '登录日志', perm: 'monitor:loginlog:list', component: LoginLogTab },
    { name: 'changelog', label: '变更日志', perm: 'monitor:changelog:list', component: ChangeLogTab }
  ].filter((t) => permStore.permissions.has(t.perm))
)
</script>

<template>
  <el-card>
    <el-tabs v-model="activeTab" class="audit-tabs">
      <el-tab-pane
        v-for="tab in tabs"
        :key="tab.name"
        :label="tab.label"
        :name="tab.name"
        lazy
      >
        <component :is="tab.component" />
      </el-tab-pane>
    </el-tabs>
    <el-empty v-if="tabs.length === 0" description="暂无日志查看权限，请联系管理员" :image-size="80" />
  </el-card>
</template>

<style scoped>
.audit-tabs :deep(.el-tabs__header) {
  margin-bottom: 14px;
}
</style>
