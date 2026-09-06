<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { Refresh } from '@element-plus/icons-vue'
import { getSystemMonitor } from '@/api/monitor'
import type { SystemMonitorInfo } from '@/api/monitor'

defineOptions({ name: 'MonitorSystemView' })

const loading = ref(false)
const info = ref<SystemMonitorInfo | null>(null)

async function loadData(): Promise<void> {
  loading.value = true
  try {
    info.value = await getSystemMonitor()
  } finally {
    loading.value = false
  }
}

const items = () => {
  const i = info.value
  if (!i) return []
  return [
    { label: '机器名', value: i.machineName },
    { label: '进程架构', value: i.processArchitecture },
    { label: '运行时长', value: i.uptimeHours.toFixed(1) + ' 小时' },
    { label: '进程内存', value: i.workingSetMb + ' MB' },
    { label: 'GC 托管内存', value: i.gcMemoryMb + ' MB' },
    { label: '线程数', value: String(i.threadCount) },
    { label: '句柄数', value: String(i.handleCount) },
    { label: 'GC 回收 (Gen0/1/2)', value: i.gen0Collections + ' / ' + i.gen1Collections + ' / ' + i.gen2Collections },
    { label: '缓存提供方', value: i.cacheProvider }
  ]
}

onMounted(loadData)
</script>

<template>
  <el-card v-loading="loading">
    <template #header>
      <div class="header-row">
        <span>系统监控</span>
        <el-button :icon="Refresh" size="small" @click="loadData">刷新</el-button>
      </div>
    </template>

    <el-descriptions :column="3" border>
      <el-descriptions-item v-for="item in items()" :key="item.label" :label="item.label">
        {{ item.value }}
      </el-descriptions-item>
    </el-descriptions>

    <template v-if="info?.cacheDiagnostics && Object.keys(info.cacheDiagnostics).length > 2">
      <h4 class="section">缓存诊断（Redis）</h4>
      <el-descriptions :column="3" border>
        <el-descriptions-item v-for="(value, key) in info.cacheDiagnostics" :key="key" :label="String(key)">
          {{ value }}
        </el-descriptions-item>
      </el-descriptions>
    </template>
    <el-alert
      v-else-if="info"
      type="info"
      :closable="false"
      class="cache-note"
      title="当前为进程内缓存（MemoryCache），无外部诊断指标。切换 Redis 后此处展示内存/命中率等指标。"
    />
  </el-card>
</template>

<style scoped>
.header-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.section {
  margin: 20px 0 10px;
}

.cache-note {
  margin-top: 16px;
}
</style>
