<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Refresh } from '@element-plus/icons-vue'
import request from '@/api/request'
import { getUserList } from '@/api/user'
import type { User } from '@/types/api'
import { formatDateTime } from '@/utils/format'
import { usePageList } from '@/composables/usePageList'

defineOptions({ name: 'FlowDelegateView' })

interface DelegateRow {
  id: string
  delegatorName: string
  agentId: string
  agentName: string
  startTime: string
  endTime: string
  status: number
  remark?: string
  createTime: string
}

const {
  loading,
  firstLoading,
  list,
  total,
  query,
  loadData
} = usePageList<DelegateRow, { pageIndex: number; pageSize: number }>({
  url: '/sys/flow/delegate',
  defaultQuery: { pageIndex: 1, pageSize: 10 }
})

// ---------- 新建委托 ----------
const dialogVisible = ref(false)
const saving = ref(false)
const formRef = ref()
const users = ref<User[]>([])
const range = ref<[string, string] | null>(null)
const form = reactive({ agentId: undefined as number | undefined, remark: '' })
const rules = {
  agentId: [{ required: true, message: '请选择代理人', trigger: 'change' }]
}

function openCreate(): void {
  form.agentId = undefined
  form.remark = ''
  range.value = null
  dialogVisible.value = true
}

async function handleSave(): Promise<void> {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return
  if (!range.value?.[0] || !range.value?.[1]) {
    ElMessage.warning('请选择委托时间段')
    return
  }
  saving.value = true
  try {
    await request.post<never, string>('/sys/flow/delegate', {
      agentId: form.agentId,
      startTime: range.value[0],
      endTime: range.value[1],
      remark: form.remark || undefined
    })
    ElMessage.success('委托已创建：时间段内的新审批将转由代理人处理')
    dialogVisible.value = false
    loadData()
  } finally {
    saving.value = false
  }
}

async function handleDelete(row: DelegateRow): Promise<void> {
  await ElMessageBox.confirm(`确定删除委托给「${row.agentName}」的这条委托吗？`, '提示', { type: 'warning' })
  await request.delete<never, void>(`/sys/flow/delegate/${row.id}`)
  ElMessage.success('删除成功')
  loadData()
}

/** 当前是否生效中 */
function active(row: DelegateRow): boolean {
  const now = Date.now()
  return row.status === 1 && new Date(row.startTime).getTime() <= now && new Date(row.endTime).getTime() > now
}

onMounted(() => {
  loadData()
  getUserList().then((list) => (users.value = list))
})
  import TableEmpty from '@/components/TableEmpty.vue'
  import TableSkeleton from '@/components/TableSkeleton.vue'
</script>

<template>
  <el-card>
    <el-alert
      type="info"
      :closable="false"
      class="tip"
      title="委托生效期间，新到达的审批待办将自动转由代理人处理；已生成的待办不受影响。适合请假/出差场景。"
    />
    <div class="toolbar">
      <el-button type="primary" :icon="Plus" @click="openCreate">新建委托</el-button>
      <el-button :icon="Refresh" @click="loadData">刷新</el-button>
    </div>

    <TableSkeleton v-if="firstLoading" />
      <el-table v-else v-loading="loading" :data="list" border stripe>
      <el-table-column prop="agentName" label="代理人" min-width="110" />
      <el-table-column label="生效时间" min-width="300">
        <template #default="{ row }">
          {{ (row as DelegateRow).startTime.replace('T', ' ') }} ~ {{ (row as DelegateRow).endTime.replace('T', ' ') }}
        </template>
      </el-table-column>
      <el-table-column label="状态" width="90" align="center">
        <template #default="{ row }">
          <el-tag :type="active(row as DelegateRow) ? 'success' : 'info'" size="small">
            {{ active(row as DelegateRow) ? '生效中' : '未生效/已过期' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="remark" label="备注" min-width="130" show-overflow-tooltip />
      <el-table-column prop="createTime" label="创建时间" width="165" :formatter="formatDateTime" />
      <el-table-column label="操作" width="80" align="center">
        <template #default="{ row }">
          <el-button link type="danger" @click="handleDelete(row as DelegateRow)">删除</el-button>
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
      layout="total, prev, pager, next"
      :total="total"
      @current-change="loadData"
    />

    <el-dialog v-model="dialogVisible" title="新建委托" width="500px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="90px">
        <el-form-item label="代理人" prop="agentId">
          <el-select v-model="form.agentId" filterable placeholder="选择代理人" style="width: 100%">
            <el-option v-for="u in users" :key="u.id" :label="u.nickName || u.userName" :value="u.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="时间段">
          <el-date-picker
            v-model="range"
            type="datetimerange"
            range-separator="至"
            start-placeholder="开始时间"
            end-placeholder="结束时间"
            value-format="YYYY-MM-DDTHH:mm:ss"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="form.remark" type="textarea" :rows="2" maxlength="200" placeholder="如：休年假" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">创建</el-button>
      </template>
    </el-dialog>
  </el-card>
</template>

<style scoped>
.tip {
  margin-bottom: 12px;
}

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
