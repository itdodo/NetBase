<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Refresh, Search } from '@element-plus/icons-vue'
import type { LoginLogInfo } from '@/api/log'
import { cleanupLoginLogs } from '@/api/monitor'
import { formatDateTime } from '@/utils/format'
import { download } from '@/utils/download'
import { usePageList } from '@/composables/usePageList'

defineOptions({ name: 'AuditLoginLogTab' })

interface LoginLogQuery {
  pageIndex: number
  pageSize: number
  keyword: string
  success?: number
  beginTime?: string
  endTime?: string
}

const dateRange = ref<[string, string] | null>(null)
const {
  loading,
  firstLoading,
  list,
  total,
  query,
  loadData,
  handleSearch
} = usePageList<LoginLogInfo, LoginLogQuery>({
  url: '/sys/log/login/page',
  defaultQuery: {
    pageIndex: 1,
    pageSize: 10,
    keyword: '',
    success: undefined,
    beginTime: undefined,
    endTime: undefined
  }
})

function handleSearchWithDate(): void {
  query.beginTime = dateRange.value?.[0]
  query.endTime = dateRange.value?.[1]
  handleSearch()
}

async function handleExport(): Promise<void> {
  await download('/sys/log/login/export', { ...query }, `登录日志_${new Date().toISOString().slice(0, 10)}.xlsx`)
}

function handleReset(): void {
  query.keyword = ''
  query.success = undefined
  dateRange.value = null
  handleSearchWithDate()
}

async function handleCleanup(): Promise<void> {
  await ElMessageBox.confirm('将物理删除 30 天前的日志，确定吗？', '清理日志', { type: 'warning' })
  const count = await cleanupLoginLogs(30)
  ElMessage.success('已清理 ' + count + ' 条')
  loadData()
}

onMounted(loadData)
  import TableEmpty from '@/components/TableEmpty.vue'
  import TableSkeleton from '@/components/TableSkeleton.vue'
</script>

<template>
    <div class="toolbar">
      <el-input
        v-model="query.keyword"
        placeholder="用户名/消息/IP"
        clearable
        style="width: 200px"
        :prefix-icon="Search"
        @keyup.enter="handleSearchWithDate"
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
      <el-button type="primary" :icon="Search" @click="handleSearchWithDate">查询</el-button>
      <el-button :icon="Refresh" @click="handleReset">重置</el-button>
      <el-button type="danger" plain @click="handleCleanup">清理</el-button>
      <el-button type="warning" @click="handleExport">导出</el-button>
    </div>

    <TableSkeleton v-if="firstLoading" />
      <el-table v-else v-loading="loading" :data="list" border stripe>
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
    <template #empty>
      <TableEmpty />
    </template>
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
