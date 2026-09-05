<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { kickSession, getSessionPage } from '@/api/auth'
import type { SessionInfo } from '@/api/auth'
import { formatDateTime } from '@/utils/format'

defineOptions({ name: 'MonitorOnlineView' })

const loading = ref(false)
const list = ref<SessionInfo[]>([])
const total = ref(0)
const query = reactive({ pageIndex: 1, pageSize: 10 })

async function loadData(): Promise<void> {
  loading.value = true
  try {
    const page = await getSessionPage(query)
    list.value = page.items
    total.value = page.total
  } finally {
    loading.value = false
  }
}

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

onMounted(loadData)
</script>

<template>
  <el-card>
    <el-table v-loading="loading" :data="list" border stripe>
      <el-table-column prop="userName" label="用户名" min-width="110" />
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
      @current-change="loadData"
    />
  </el-card>
</template>

<style scoped>
.pagination {
  margin-top: 12px;
  justify-content: flex-end;
}
</style>
