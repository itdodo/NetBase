<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Refresh, Search } from '@element-plus/icons-vue'
import type { FlowTaskView } from '@/api/flow'
import { withdrawFlowInstance } from '@/api/flow'
import { formatDateTime } from '@/utils/format'
import { usePageList } from '@/composables/usePageList'
import FlowTimeline from '@/components/flow/FlowTimeline.vue'
import BizDocPreview from '@/components/flow/BizDocPreview.vue'
import ApprovalActions from '@/components/flow/ApprovalActions.vue'

defineOptions({ name: 'FlowTodoView' })

const {
  loading,
  list,
  total,
  query,
  loadData
} = usePageList<FlowTaskView, { pageIndex: number; pageSize: number; keyword: string; flowCode: string }>({
  url: '/sys/flow/task/todo',
  defaultQuery: { pageIndex: 1, pageSize: 10, keyword: '', flowCode: '' }
})

function handleSearch(): void {
  loadData()
}

function handleReset(): void {
  query.keyword = ''
  query.flowCode = ''
  handleSearch()
}

// ---------- 审批详情抽屉 ----------
const detailVisible = ref(false)
const activeTab = ref('doc')
const current = ref<FlowTaskView | null>(null)

function openDetail(row: FlowTaskView): void {
  current.value = row
  activeTab.value = 'doc'
  detailVisible.value = true
}

async function onActed(): Promise<void> {
  ElMessage.success('操作成功')
  detailVisible.value = false
  loadData()
}

async function handleWithdraw(): Promise<void> {
  if (!current.value) return
  await ElMessageBox.confirm(`确定撤回「${current.value.summary}」的审批吗？`, '提示', { type: 'warning' })
  await withdrawFlowInstance(current.value.instanceId)
  ElMessage.success('已撤回')
  detailVisible.value = false
  loadData()
}

onMounted(loadData)
</script>

<template>
  <el-card>
    <div class="toolbar">
      <el-input
        v-model="query.keyword"
        placeholder="标题/提交人"
        clearable
        style="width: 180px"
        :prefix-icon="Search"
        @keyup.enter="handleSearch"
      />
      <el-input v-model="query.flowCode" placeholder="流程编码" clearable style="width: 140px" @keyup.enter="handleSearch" />
      <el-button type="primary" :icon="Search" @click="handleSearch">查询</el-button>
      <el-button :icon="Refresh" @click="handleReset">重置</el-button>
    </div>
    <el-table v-loading="loading" :data="list" border stripe>
      <el-table-column prop="summary" label="待办标题" min-width="200" show-overflow-tooltip>
        <template #default="{ row }">
          <el-link type="primary" @click="openDetail(row as FlowTaskView)">{{ (row as FlowTaskView).summary }}</el-link>
        </template>
      </el-table-column>
      <el-table-column prop="nodeName" label="当前环节" min-width="120" />
      <el-table-column prop="flowCode" label="流程" min-width="110" />
      <el-table-column prop="submitterName" label="提交人" min-width="100" />
      <el-table-column prop="submitTime" label="提交时间" width="165" :formatter="formatDateTime" />
      <el-table-column label="操作" width="100" align="center">
        <template #default="{ row }">
          <el-button link type="primary" @click="openDetail(row as FlowTaskView)">审批</el-button>
        </template>
      </el-table-column>
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
        <div class="meta">
          <span>流程：{{ current.flowCode }}</span>
          <span>提交人：{{ current.submitterName }}</span>
          <span>当前环节：{{ current.nodeName }}</span>
        </div>
        <el-tabs v-model="activeTab">
          <el-tab-pane label="业务单据" name="doc">
            <BizDocPreview :business-table="current.businessTable" :business-id="current.businessId" />
          </el-tab-pane>
          <el-tab-pane label="审批记录" name="flow">
            <ApprovalActions :task-id="current.taskId" :instance-id="current.instanceId" @acted="onActed" />
            <el-divider />
            <FlowTimeline :instance-id="current.instanceId" />
            <div class="withdraw-row">
              <el-button link type="warning" @click="handleWithdraw">撤回此审批</el-button>
            </div>
          </el-tab-pane>
        </el-tabs>
      </template>
    </el-drawer>
  </el-card>
</template>

<style scoped>
.toolbar {
  display: flex;
  gap: 8px;
  margin-bottom: 12px;
  flex-wrap: wrap;
}

.pagination {
  margin-top: 12px;
  justify-content: flex-end;
}

.meta {
  display: flex;
  gap: 14px;
  margin-bottom: 12px;
  color: var(--el-text-color-secondary);
  font-size: 13px;
}

.withdraw-row {
  margin-top: 16px;
  text-align: center;
}
</style>
