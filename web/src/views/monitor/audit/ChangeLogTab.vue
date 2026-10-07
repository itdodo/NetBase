<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Refresh, Search, View } from '@element-plus/icons-vue'
import type { ChangeLogInfo } from '@/api/changelog'
import { cleanupChangeLogs } from '@/api/changelog'
import { formatDateTime } from '@/utils/format'
import { usePageList } from '@/composables/usePageList'

defineOptions({ name: 'AuditChangeLogTab' })

interface ChangeLogQueryLocal {
  pageIndex: number
  pageSize: number
  tableName: string
  userName: string
  beginTime?: string
  endTime?: string
}

// 列表样板复用 usePageList（加载/分页/查询），本页仅保留时间范围、明细查看与清理逻辑
const dateRange = ref<[string, string] | null>(null)
const {
  loading,
  firstLoading,
  list,
  total,
  query,
  loadData,
  handleSearch
} = usePageList<ChangeLogInfo, ChangeLogQueryLocal>({
  url: '/sys/changelog/page',
  defaultQuery: {
    pageIndex: 1,
    pageSize: 10,
    tableName: '',
    userName: '',
    beginTime: undefined,
    endTime: undefined
  }
})

function handleSearchWithDate(): void {
  query.beginTime = dateRange.value?.[0]
  query.endTime = dateRange.value?.[1]
  handleSearch()
}

function handleReset(): void {
  query.tableName = ''
  query.userName = ''
  dateRange.value = null
  handleSearchWithDate()
}

async function handleCleanup(): Promise<void> {
  await ElMessageBox.confirm('将物理删除 30 天前的变更日志，确定吗？', '清理变更日志', { type: 'warning' })
  const count = await cleanupChangeLogs(30)
  ElMessage.success('已清理 ' + count + ' 条')
  loadData()
}

// ---------- 变更明细查看 ----------
const detailVisible = ref(false)
const detailRow = ref<ChangeLogInfo | null>(null)

/** 变更明细 JSON 转为可读文本（解析失败时原样展示） */
function prettyChanges(changes: string): string {
  try {
    return JSON.stringify(JSON.parse(changes), null, 2)
  } catch {
    return changes
  }
}

function showDetail(row: ChangeLogInfo): void {
  detailRow.value = row
  detailVisible.value = true
}

onMounted(loadData)
  import TableEmpty from '@/components/TableEmpty.vue'
  import TableSkeleton from '@/components/TableSkeleton.vue'
</script>

<template>
    <div class="toolbar">
      <el-input
        v-model="query.tableName"
        placeholder="表名（如 SysRole）"
        clearable
        style="width: 180px"
        :prefix-icon="Search"
        @keyup.enter="handleSearchWithDate"
      />
      <el-input
        v-model="query.userName"
        placeholder="操作人"
        clearable
        style="width: 140px"
        @keyup.enter="handleSearchWithDate"
      />
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
    </div>

    <TableSkeleton v-if="firstLoading" />
      <el-table v-else v-loading="loading" :data="list" border stripe>
      <el-table-column prop="tableName" label="表名" min-width="110" />
      <el-table-column prop="recordId" label="记录ID" min-width="170" show-overflow-tooltip />
      <el-table-column prop="userName" label="操作人" min-width="100" />
      <el-table-column prop="createTime" label="时间" width="165" :formatter="formatDateTime" />
      <el-table-column label="变更明细" min-width="200">
        <template #default="{ row }">
          <span class="changes-preview">{{ row.changes }}</span>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="80" align="center">
        <template #default="{ row }">
          <el-button link type="primary" :icon="View" @click="showDetail(row as ChangeLogInfo)">明细</el-button>
        </template>
      </el-table-column>
    <template #empty>
      <TableEmpty />
    </template>
    </el-table>

    <el-pagination
      v-model:current-page="query.pageIndex"
      v-model:page-size="query.pageSize"
      class="pagination"
      background
      layout="total, sizes, prev, pager, next, jumper"
      :page-sizes="[10, 20, 50, 100]"
        :total="total"
      @size-change="() => { query.pageIndex = 1; loadData() }"
          @current-change="loadData"
    />

    <el-dialog v-model="detailVisible" :title="`变更明细：${detailRow?.tableName ?? ''}`" width="560px">
      <div v-if="detailRow" class="detail-meta">
        <span>记录ID：{{ detailRow.recordId }}</span>
        <span>操作人：{{ detailRow.userName || '系统' }}</span>
        <span>时间：{{ detailRow.createTime }}</span>
      </div>
      <pre v-if="detailRow" class="detail-json">{{ prettyChanges(detailRow.changes) }}</pre>
    </el-dialog>
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

.changes-preview {
  display: inline-block;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  vertical-align: middle;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.detail-meta {
  display: flex;
  gap: 16px;
  margin-bottom: 8px;
  color: var(--el-text-color-secondary);
  font-size: 13px;
}

.detail-json {
  margin: 0;
  padding: 12px;
  background: var(--el-fill-color-light);
  border-radius: 4px;
  font-size: 12px;
  line-height: 1.6;
  max-height: 420px;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-all;
}
</style>
