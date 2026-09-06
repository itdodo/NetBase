<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { Refresh, Search } from '@element-plus/icons-vue'
import { getLoginLogPage } from '@/api/log'
import type { LoginLogInfo } from '@/api/log'
import { formatDateTime } from '@/utils/format'
import { cleanupLoginLogs } from '@/api/monitor'
import { download } from '@/utils/download'

defineOptions({ name: 'MonitorLoginlogView' })

const loading = ref(false)
const list = ref<LoginLogInfo[]>([])
const total = ref(0)

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  success: undefined as number | undefined,
  beginTime: undefined as string | undefined,
  endTime: undefined as string | undefined
})
const dateRange = ref<[string, string] | null>(null)

async function loadData(): Promise<void> {
  loading.value = true
  try {
    const page = await getLoginLogPage(query)
    list.value = page.items
    total.value = page.total
  } finally {
    loading.value = false
  }
}

function handleSearch(): void {
  query.pageIndex = 1
  query.beginTime = dateRange.value?.[0]
  query.endTime = dateRange.value?.[1]
  loadData()
}

async function handleExport(): Promise<void> {
  await download('/sys/log/login/export', { ...query }, `登录日志_${new Date().toISOString().slice(0, 10)}.xlsx`)
}

function handleReset(): void {
  query.keyword = ''
  query.success = undefined
  dateRange.value = null
  handleSearch()
}

async function handleCleanup(): Promise<void> {
  await ElMessageBox.confirm('将物理删除 30 天前的日志，确定吗？', '清理日志', { type: 'warning' })
  const count = await cleanupLoginLogs(30)
  ElMessage.success('已清理 ' + count + ' 条')
  loadData()
}

onMounted(loadData)
</script>

<template>
  <el-card>
    <div class="toolbar">
      <el-input
        v-model="query.keyword"
        placeholder="用户名/消息/IP"
        clearable
        style="width: 200px"
        :prefix-icon="Search"
        @keyup.enter="handleSearch"
      />
      <el-select v-model="query.success" placeholder="结果" clearable style="width: 120px">
        <el-option label="成功" :value="1" />
        <el-option label="失败" :value="0" />
      </el-select>
      <el-date-picker
        v-model="dateRange"
        type="daterange"
        range-separator="-"
        start-placeholder="开始日期"
        end-placeholder="结束日期"
        value-format="YYYY-MM-DD"
        style="width: 240px"
      />
      <el-button type="primary" :icon="Search" @click="handleSearch">查询</el-button>
      <el-button :icon="Refresh" @click="handleReset">重置</el-button>
      <el-button type="danger" plain @click="handleCleanup">清理</el-button>
      <el-button v-permission="'monitor:loginlog:list'" type="warning" @click="handleExport">导出</el-button>
    </div>

    <el-table v-loading="loading" :data="list" border stripe>
      <el-table-column prop="userName" label="用户名" min-width="110" />
      <el-table-column label="结果" width="80" align="center">
        <template #default="{ row }">
          <el-tag :type="row.success ? 'success' : 'danger'" size="small">
            {{ row.success ? '成功' : '失败' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="message" label="结果描述" min-width="180" show-overflow-tooltip />
      <el-table-column prop="ip" label="登录IP" min-width="130" />
      <el-table-column prop="userAgent" label="浏览器标识" min-width="220" show-overflow-tooltip />
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
