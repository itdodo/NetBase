<script setup lang="ts">
import { onMounted, ref } from 'vue'
import type { FlowInstance } from '@/api/flow'
import { FLOW_INSTANCE_STATUS } from '@/api/flow'
import { formatDateTime } from '@/utils/format'
import { usePageList } from '@/composables/usePageList'
import FlowTimeline from '@/components/flow/FlowTimeline.vue'
import BizDocPreview from '@/components/flow/BizDocPreview.vue'

defineOptions({ name: 'FlowCcView' })

const {
  loading,
  list,
  total,
  query,
  loadData
} = usePageList<FlowInstance, { pageIndex: number; pageSize: number }>({
  url: '/sys/flow/instance/cc-me',
  defaultQuery: { pageIndex: 1, pageSize: 10 }
})

const detailVisible = ref(false)
const activeTab = ref('doc')
const current = ref<FlowInstance | null>(null)

function openDetail(row: FlowInstance): void {
  current.value = row
  activeTab.value = 'doc'
  detailVisible.value = true
}

onMounted(loadData)
</script>

<template>
  <el-card>
    <el-table v-loading="loading" :data="list" border stripe>
      <el-table-column prop="summary" label="标题" min-width="220" show-overflow-tooltip>
        <template #default="{ row }">
          <el-link type="primary" @click="openDetail(row as FlowInstance)">{{ (row as FlowInstance).summary }}</el-link>
        </template>
      </el-table-column>
      <el-table-column prop="flowCode" label="流程" min-width="120" />
      <el-table-column label="状态" width="90" align="center">
        <template #default="{ row }">
          <el-tag :type="FLOW_INSTANCE_STATUS[(row as FlowInstance).status]?.type ?? 'info'" size="small">
            {{ FLOW_INSTANCE_STATUS[(row as FlowInstance).status]?.label }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="submitterName" label="提交人" min-width="100" />
      <el-table-column prop="createTime" label="抄送时间" width="165" :formatter="formatDateTime" />
    </el-table>

    <el-pagination
      v-model:current-page="query.pageIndex"
      v-model:page-size="query.pageSize"
      class="pagination"
      background
      layout="total, prev, pager, next"
      :total="total"
      @current-change="loadData"
    />

    <el-drawer v-model="detailVisible" :title="current?.summary" size="520px">
      <template v-if="current">
        <el-tabs v-model="activeTab">
          <el-tab-pane label="业务单据" name="doc">
            <BizDocPreview :business-table="current.businessTable" :business-id="current.businessId" />
          </el-tab-pane>
          <el-tab-pane label="审批记录" name="flow">
            <FlowTimeline :instance-id="current.id" />
          </el-tab-pane>
        </el-tabs>
      </template>
    </el-drawer>
  </el-card>
</template>

<style scoped>
.pagination {
  margin-top: 12px;
  justify-content: flex-end;
}
</style>
