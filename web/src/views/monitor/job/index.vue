<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Refresh } from '@element-plus/icons-vue'
import { getJobList, pauseJob, resumeJob, triggerJob, updateJobCron } from '@/api/job'
import type { JobInstanceInfo } from '@/api/job'
import { formatDateTime } from '@/utils/format'

defineOptions({ name: 'MonitorJobView' })

const loading = ref(false)
const list = ref<JobInstanceInfo[]>([])

async function loadData(): Promise<void> {
  loading.value = true
  try {
    list.value = await getJobList()
  } finally {
    loading.value = false
  }
}

// ---------- 立即执行 ----------
async function handleTrigger(row: JobInstanceInfo): Promise<void> {
  await ElMessageBox.confirm(`确定立即执行作业「${row.displayName}」吗？`, '触发执行', { type: 'warning' })
  await triggerJob(row.jobId)
  ElMessage.success('已触发执行')
}

// ---------- 暂停 / 恢复 ----------
async function handlePause(row: JobInstanceInfo): Promise<void> {
  await ElMessageBox.confirm(`确定暂停作业「${row.displayName}」吗？暂停后不再按 Cron 调度。`, '暂停作业', { type: 'warning' })
  await pauseJob(row.jobId)
  ElMessage.success('已暂停')
  loadData()
}

async function handleResume(row: JobInstanceInfo): Promise<void> {
  await resumeJob(row.jobId)
  ElMessage.success('已恢复调度')
  loadData()
}

// ---------- 修改 Cron ----------
const cronDialogVisible = ref(false)
const editingJob = ref<JobInstanceInfo | null>(null)
const cronInput = ref('')
const cronSaving = ref(false)

function openEditCron(row: JobInstanceInfo): void {
  editingJob.value = row
  cronInput.value = row.cron
  cronDialogVisible.value = true
}

async function handleSaveCron(): Promise<void> {
  if (!editingJob.value) return
  const cron = cronInput.value.trim()
  if (cron.split(/\s+/).length !== 5) {
    ElMessage.error('Cron 须为 5 段式表达式（分 时 日 月 周）')
    return
  }
  cronSaving.value = true
  try {
    await updateJobCron(editingJob.value.jobId, cron)
    ElMessage.success('调度已更新')
    cronDialogVisible.value = false
    loadData()
  } finally {
    cronSaving.value = false
  }
}

onMounted(loadData)
</script>

<template>
  <el-card>
    <div class="toolbar">
      <span class="title">定时任务（Hangfire）</span>
      <el-button :icon="Refresh" @click="loadData">刷新</el-button>
    </div>

    <el-table v-loading="loading" :data="list" border stripe>
      <el-table-column prop="jobId" label="作业标识" min-width="140" />
      <el-table-column prop="displayName" label="名称" min-width="240" show-overflow-tooltip />
      <el-table-column prop="cron" label="Cron" min-width="110" />
      <el-table-column label="上次执行" width="165" :formatter="formatDateTime" prop="lastExecution" />
      <el-table-column label="上次结果" width="100" align="center">
        <template #default="{ row }">
          <el-tag
            v-if="row.lastJobSuccess !== undefined && row.lastJobSuccess !== null"
            :type="row.lastJobSuccess ? 'success' : 'danger'"
            size="small"
          >
            {{ row.lastJobSuccess ? '成功' : '失败' }}
          </el-tag>
          <span v-else>-</span>
        </template>
      </el-table-column>
      <el-table-column label="下次执行" width="165" :formatter="formatDateTime" prop="nextExecution" />
      <el-table-column label="状态" width="90" align="center">
        <template #default="{ row }">
          <el-tag :type="row.paused ? 'warning' : 'success'" size="small">
            {{ row.paused ? '已暂停' : '运行中' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="220" align="center">
        <template #default="{ row }">
          <el-button v-permission="'monitor:job:trigger'" link type="success" @click="handleTrigger(row as JobInstanceInfo)">
            立即执行
          </el-button>
          <el-button v-permission="'monitor:job:edit'" link type="primary" @click="openEditCron(row as JobInstanceInfo)">
            修改Cron
          </el-button>
          <el-button v-if="!row.paused" v-permission="'monitor:job:edit'" link type="warning" @click="handlePause(row as JobInstanceInfo)">
            暂停
          </el-button>
          <el-button v-else v-permission="'monitor:job:edit'" link type="success" @click="handleResume(row as JobInstanceInfo)">
            恢复
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 修改 Cron -->
    <el-dialog v-model="cronDialogVisible" :title="`修改调度：${editingJob?.displayName}`" width="440px">
      <el-input v-model="cronInput" placeholder="5 段式 Cron，如 0 2 * * *" />
      <div class="cron-tip">5 段式：分 时 日 月 周。示例 <code>0 2 * * *</code> = 每日 02:00</div>
      <template #footer>
        <el-button @click="cronDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="cronSaving" @click="handleSaveCron">确定</el-button>
      </template>
    </el-dialog>
  </el-card>
</template>

<style scoped>
.toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.title {
  font-weight: 600;
}

.cron-tip {
  font-size: 12px;
  color: #909399;
  margin-top: 8px;
}
</style>
