<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Download, Refresh, VideoPlay } from '@element-plus/icons-vue'
import type { BackupFile } from '@/api/monitor/backup'
import { downloadBackupFile, getBackupFiles, runBackup } from '@/api/monitor/backup'
import { formatDateTime } from '@/utils/format'

defineOptions({ name: 'MonitorBackupView' })

const loading = ref(false)
const running = ref(false)
const files = ref<BackupFile[]>([])

async function loadData(): Promise<void> {
  loading.value = true
  try {
    files.value = await getBackupFiles()
  } finally {
    loading.value = false
  }
}

async function handleRun(): Promise<void> {
  await ElMessageBox.confirm('立即执行一次全量备份？（数据量大时可能耗时较长）', '提示', { type: 'info' })
  running.value = true
  try {
    const result = await runBackup()
    ElMessage.success(result || '备份完成')
    loadData()
  } finally {
    running.value = false
  }
}

async function handleDownload(row: BackupFile): Promise<void> {
  const blob = await downloadBackupFile(row.fileName)
  const a = document.createElement('a')
  a.href = URL.createObjectURL(blob)
  a.download = row.fileName
  a.click()
  URL.revokeObjectURL(a.href)
}

function sizeText(bytes: number): string {
  if (bytes >= 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`
  if (bytes >= 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`
  return `${(bytes / 1024).toFixed(1)} KB`
}

onMounted(loadData)
</script>

<template>
  <el-card>
    <el-alert
      type="info"
      :closable="false"
      style="margin-bottom: 12px"
      title="每日 03:00 自动全量备份（pg_dump custom 格式，保留 7 天）；备份含上传文件镜像。恢复操作手册见 docs/备份恢复.md"
    />

    <div class="toolbar">
      <el-button v-permission="'monitor:backup:run'" type="primary" :icon="VideoPlay" :loading="running" @click="handleRun">
        立即备份
      </el-button>
      <el-button :icon="Refresh" @click="loadData">刷新</el-button>
    </div>

    <el-table v-loading="loading" :data="files" border stripe>
      <el-table-column prop="fileName" label="备份文件" min-width="280" show-overflow-tooltip />
      <el-table-column label="大小" width="110" align="right">
        <template #default="{ row }">{{ sizeText((row as BackupFile).sizeBytes) }}</template>
      </el-table-column>
      <el-table-column label="备份时间" width="180">
        <template #default="{ row }">{{ formatDateTime(null, null, (row as BackupFile).lastWriteTime) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="100" align="center">
        <template #default="{ row }">
          <el-button v-permission="'monitor:backup:list'" link type="primary" :icon="Download" @click="handleDownload(row as BackupFile)">下载</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-empty v-if="!loading && files.length === 0" description="暂无备份文件（每日 03:00 自动生成，或点「立即备份」）" :image-size="80" />
  </el-card>
</template>

<style scoped>
.toolbar {
  display: flex;
  gap: 8px;
  margin-bottom: 12px;
}
</style>
