<script setup lang="ts">
import { onActivated, onMounted, onUnmounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Refresh } from '@element-plus/icons-vue'
import { kickSession, kickSessionsBatch, getSessionPage } from '@/api/auth'
import type { SessionInfo } from '@/api/auth'
import { formatDateTime } from '@/utils/format'

defineOptions({ name: 'MonitorOnlineView' })

const loading = ref(false)
const list = ref<SessionInfo[]>([])
const total = ref(0)
const query = reactive({ pageIndex: 1, pageSize: 10 })
const selection = ref<SessionInfo[]>([])

async function loadData(silent = false): Promise<void> {
  if (!silent) loading.value = true
  try {
    const page = await getSessionPage(query)
    list.value = page.items
    total.value = page.total
  } finally {
    loading.value = false
  }
}

/** 自动轮询：在线用户是实时视图，15 秒静默刷新（不闪加载动画），可开关 */
const autoRefresh = ref(true)
let refreshTimer: ReturnType<typeof setInterval> | null = null

function setAutoRefresh(on: boolean | string | number): void {
  if (refreshTimer) {
    clearInterval(refreshTimer)
    refreshTimer = null
  }
  if (on === true) {
    refreshTimer = setInterval(() => loadData(true), 15000)
  }
}

onMounted(() => {
  loadData()
  setAutoRefresh(autoRefresh.value)
})

// 页签 keep-alive：切回本页时立即刷新，杜绝陈旧数据
onActivated(() => loadData(true))

onUnmounted(() => setAutoRefresh(false))

async function handleKick(row: SessionInfo): Promise<void> {
  await ElMessageBox.confirm(
    `确定强制下线用户「${row.userName}」的该会话吗？`,
    '强制下线',
    { type: 'warning' },
  )
  await kickSession(row.id)
  ElMessage.success('已强制下线')
  loadData()
}

/** 批量强制下线（后端自动排除当前请求自己的会话，双保险） */
async function handleBatchKick(): Promise<void> {
  const ids = selection.value.filter(s => !s.isCurrent).map(s => String(s.id))
  if (ids.length === 0) {
    ElMessage.warning('请先勾选要下线的会话（当前会话不可选）')
    return
  }
  await ElMessageBox.confirm(`确定强制下线选中的 ${ids.length} 个会话吗？`, '批量强制下线', { type: 'warning' })
  const msg = await kickSessionsBatch(ids)
  ElMessage.success(msg || '已批量强制下线')
  loadData()
}

</script>

<template>
  <el-card>
    <div class="toolbar">
      <el-button type="primary" plain :icon="Refresh" @click="loadData()">刷新</el-button>
      <el-button
        v-permission="'monitor:online:list'"
        type="danger"
        plain
        :disabled="selection.filter(s => !s.isCurrent).length === 0"
        @click="handleBatchKick"
      >
        批量下线{{ selection.filter(s => !s.isCurrent).length > 0 ? `（${selection.filter(s => !s.isCurrent).length}）` : '' }}
      </el-button>
      <span class="auto-refresh">
        <el-switch v-model="autoRefresh" @change="setAutoRefresh" />
        15 秒自动刷新
      </span>
    </div>
    <el-table v-loading="loading" :data="list" border stripe @selection-change="selection = $event as SessionInfo[]">
      <el-table-column type="selection" width="45" :selectable="(row: SessionInfo) => !row.isCurrent" />
      <el-table-column prop="userName" label="用户名" min-width="110">
        <template #default="{ row }">
          {{ row.userName }}
          <el-tag v-if="row.isCurrent" size="small" type="info">当前</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="nickName" label="昵称" min-width="110" />
      <el-table-column prop="loginIp" label="登录IP" min-width="130" />
      <el-table-column prop="userAgent" label="浏览器标识" min-width="200" show-overflow-tooltip />
      <el-table-column prop="loginTime" label="登录时间" width="165" :formatter="formatDateTime" />
      <el-table-column prop="expireTime" label="过期时间" width="165" :formatter="formatDateTime" />
      <el-table-column label="操作" width="110" align="center">
        <template #default="{ row }">
          <el-button v-permission="'monitor:online:list'" link type="danger" @click="handleKick(row as SessionInfo)">
            强制下线
          </el-button>
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
      @current-change="() => loadData()"
    />
  </el-card>
</template>

<style scoped>
.toolbar {
  display: flex;
  align-items: center;
  gap: 14px;
  margin-bottom: 12px;
}

.auto-refresh {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: var(--el-text-color-secondary);
  font-size: 13px;
}
.pagination {
  margin-top: 12px;
  justify-content: flex-end;
}
</style>
