<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Search } from '@element-plus/icons-vue'
import type { FlowInstance, FlowInstanceDetail } from '@/api/flow'
import { FLOW_INSTANCE_STATUS, getFlowInstanceDetail, withdrawFlowInstance } from '@/api/flow'
import { formatDateTime } from '@/utils/format'
import { usePageList } from '@/composables/usePageList'
import FlowTimeline from '@/components/flow/FlowTimeline.vue'
import BizDocPreview from '@/components/flow/BizDocPreview.vue'

defineOptions({ name: 'FlowMineView' })

const {
  loading,
  list,
  total,
  query,
  loadData,
  handleSearch
} = usePageList<FlowInstance, { pageIndex: number; pageSize: number; flowCode: string; status?: number }>({
  url: '/sys/flow/instance/my',
  defaultQuery: { pageIndex: 1, pageSize: 10, flowCode: '', status: undefined }
})

function handleReset(): void {
  query.flowCode = ''
  query.status = undefined
  handleSearch()
}

// ---------- 详情抽屉（业务单据 / 审批记录 双页签） ----------
const detailVisible = ref(false)
const activeTab = ref('doc')
const detail = ref<FlowInstanceDetail | null>(null)
const detailLoading = ref(false)

async function openDetail(row: FlowInstance): Promise<void> {
  detailVisible.value = true
  activeTab.value = 'doc'
  detailLoading.value = true
  try {
    detail.value = await getFlowInstanceDetail(row.id)
  } finally {
    detailLoading.value = false
  }
}

async function handleWithdraw(): Promise<void> {
  if (!detail.value) return
  await ElMessageBox.confirm(`确定撤回「${detail.value.instance.summary}」吗？`, '提示', { type: 'warning' })
  await withdrawFlowInstance(detail.value.instance.id)
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
        v-model="query.flowCode"
        placeholder="流程编码"
        clearable
        style="width: 160px"
        :prefix-icon="Search"
        @keyup.enter="handleSearch"
      />
      <el-select v-model="query.status" placeholder="状态" clearable style="width: 130px">
        <el-option v-for="(v, k) in FLOW_INSTANCE_STATUS" :key="k" :label="v.label" :value="Number(k)" />
      </el-select>
      <el-button type="primary" :icon="Search" @click="handleSearch">查询</el-button>
      <el-button @click="handleReset">重置</el-button>
    </div>

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
      <el-table-column prop="submitTime" label="提交时间" width="165" :formatter="formatDateTime" />
      <el-table-column prop="endTime" label="结束时间" width="165" :formatter="formatDateTime" />
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

    <el-drawer v-model="detailVisible" :title="detail?.instance.summary" size="520px">
      <div v-loading="detailLoading">
        <template v-if="detail">
          <el-tabs v-model="activeTab">
            <el-tab-pane label="业务单据" name="doc">
              <BizDocPreview
                :business-table="detail.instance.businessTable"
                :business-id="detail.instance.businessId"
              />
            </el-tab-pane>
            <el-tab-pane label="审批记录" name="flow">
              <FlowTimeline :instance-id="detail.instance.id" />
              <div v-if="detail.canWithdraw" class="withdraw-row">
                <el-button type="warning" plain @click="handleWithdraw">撤回此审批</el-button>
              </div>
            </el-tab-pane>
          </el-tabs>
        </template>
      </div>
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

.withdraw-row {
  margin-top: 16px;
  text-align: center;
}
</style>
