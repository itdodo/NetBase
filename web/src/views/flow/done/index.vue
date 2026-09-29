<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { Refresh, Search } from '@element-plus/icons-vue'
import type { FlowTaskView } from '@/api/flow'
import { formatDateTime } from '@/utils/format'
import { usePageList } from '@/composables/usePageList'
import FlowTimeline from '@/components/flow/FlowTimeline.vue'
import BizDocPreview from '@/components/flow/BizDocPreview.vue'

defineOptions({ name: 'FlowDoneView' })

const {
  loading,
  list,
  total,
  query,
  loadData
} = usePageList<FlowTaskView, { pageIndex: number; pageSize: number; keyword: string; flowCode: string; beginTime?: string; endTime?: string }>({
  url: '/sys/flow/task/done',
  defaultQuery: { pageIndex: 1, pageSize: 10, keyword: '', flowCode: '' , beginTime: undefined, endTime: undefined }
})

const timeRange = ref<[string, string] | null>(null)

function handleSearch(): void {
  if (timeRange.value) {
    query.beginTime = timeRange.value[0]
    query.endTime = timeRange.value[1]
  } else {
    query.beginTime = undefined
    query.endTime = undefined
  }
  loadData()
}

function handleReset(): void {
  query.keyword = ''
  query.flowCode = ''
  timeRange.value = null
  handleSearch()
}

const detailVisible = ref(false)
const activeTab = ref('doc')
const current = ref<FlowTaskView | null>(null)

function openDetail(row: FlowTaskView): void {
  current.value = row
  activeTab.value = 'doc'
  detailVisible.value = true
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
      <el-date-picker
        v-model="timeRange"
        type="daterange"
        range-separator="至"
        start-placeholder="提交开始"
        end-placeholder="提交结束"
        value-format="YYYY-MM-DD"
        style="width: 260px"
      />
      <el-button type="primary" :icon="Search" @click="handleSearch">查询</el-button>
      <el-button :icon="Refresh" @click="handleReset">重置</el-button>
    </div>
    <el-table v-loading="loading" :data="list" border stripe>
      <el-table-column prop="summary" label="标题" min-width="200" show-overflow-tooltip>
        <template #default="{ row }">
          <el-link type="primary" @click="openDetail(row as FlowTaskView)">{{ (row as FlowTaskView).summary }}</el-link>
        </template>
      </el-table-column>
      <el-table-column prop="nodeName" label="审批环节" min-width="120" />
      <el-table-column prop="submitterName" label="提交人" min-width="100" />
      <el-table-column prop="actResult" label="我的处理" width="90" align="center">
        <template #default="{ row }">
          <el-tag
            :type="(row as FlowTaskView).actResult === '同意' ? 'success' : (row as FlowTaskView).actResult === '拒绝' ? 'danger' : 'info'"
            size="small"
          >
            {{ (row as FlowTaskView).actResult || '-' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="actTime" label="处理时间" width="165" :formatter="formatDateTime" />
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
      <el-tabs v-if="current" v-model="activeTab">
        <el-tab-pane label="业务单据" name="doc">
          <BizDocPreview :business-table="current.businessTable" :business-id="current.businessId" />
        </el-tab-pane>
        <el-tab-pane label="审批记录" name="flow">
          <FlowTimeline :instance-id="current.instanceId" />
        </el-tab-pane>
      </el-tabs>
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
</style>
