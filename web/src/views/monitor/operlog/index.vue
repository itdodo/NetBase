<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { Refresh, Search } from '@element-plus/icons-vue'
import { getOperationLogPage } from '@/api/log'
import type { OperationLogInfo } from '@/api/log'
import { formatDateTime } from '@/utils/format'
import { download } from '@/utils/download'

defineOptions({ name: 'MonitorOperlogView' })

const loading = ref(false)
const list = ref<OperationLogInfo[]>([])
const total = ref(0)

const query = reactive({ pageIndex: 1, pageSize: 10, keyword: '', success: undefined as number | undefined })

async function loadData(): Promise<void> {
  loading.value = true
  try {
    const page = await getOperationLogPage(query)
    list.value = page.items
    total.value = page.total
  } finally {
    loading.value = false
  }
}

function handleSearch(): void {
  query.pageIndex = 1
  loadData()
}

async function handleExport(): Promise<void> {
  await download('/sys/log/operation/export', { ...query }, `操作日志_${new Date().toISOString().slice(0, 10)}.xlsx`)
}

function handleReset(): void {
  query.keyword = ''
  query.success = undefined
  handleSearch()
}

onMounted(loadData)
</script>

<template>
  <el-card>
    <div class="toolbar">
      <el-input
        v-model="query.keyword"
        placeholder="用户名/模块/动作"
        clearable
        style="width: 200px"
        :prefix-icon="Search"
        @keyup.enter="handleSearch"
      />
      <el-select v-model="query.success" placeholder="结果" clearable style="width: 120px">
        <el-option label="成功" :value="1" />
        <el-option label="失败" :value="0" />
      </el-select>
      <el-button type="primary" :icon="Search" @click="handleSearch">查询</el-button>
      <el-button :icon="Refresh" @click="handleReset">重置</el-button>
      <el-button v-permission="'monitor:operlog:list'" type="warning" @click="handleExport">导出</el-button>
    </div>

    <el-table v-loading="loading" :data="list" border stripe>
      <el-table-column prop="userName" label="操作人" min-width="100" />
      <el-table-column prop="module" label="模块" min-width="100" />
      <el-table-column prop="action" label="动作" min-width="120" />
      <el-table-column prop="httpMethod" label="方法" width="80" align="center" />
      <el-table-column prop="path" label="路径" min-width="180" show-overflow-tooltip />
      <el-table-column prop="params" label="参数" min-width="180" show-overflow-tooltip />
      <el-table-column label="结果" width="80" align="center">
        <template #default="{ row }">
          <el-tag :type="row.success ? 'success' : 'danger'" size="small">
            {{ row.success ? '成功' : '失败' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="errorMessage" label="错误消息" min-width="140" show-overflow-tooltip />
      <el-table-column prop="elapsedMs" label="耗时(ms)" width="90" align="center" />
      <el-table-column prop="ip" label="IP" min-width="120" />
      <el-table-column prop="createTime" label="时间" width="165" :formatter="formatDateTime" />
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
