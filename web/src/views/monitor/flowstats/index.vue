<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { Refresh, Search } from '@element-plus/icons-vue'
import request from '@/api/request'

defineOptions({ name: 'MonitorFlowStatsView' })

interface StatsSummary {
  total: number
  running: number
  approved: number
  rejected: number
  avgApproveHours: number
  overduePending: number
}

interface ByFlowRow {
  flowCode: string
  total: number
  running: number
  approved: number
  rejected: number
  avgApproveHours: number
}

interface ByApproverRow {
  userName: string
  handled: number
  approved: number
  rejected: number
  pending: number
  avgHandleHours: number
}

const loading = ref(false)
const summary = ref<StatsSummary | null>(null)
const byFlow = ref<ByFlowRow[]>([])
const byApprover = ref<ByApproverRow[]>([])
const dateRange = ref<[string, string] | null>(null)

async function loadData(): Promise<void> {
  loading.value = true
  try {
    const data = await request.get<never, {
      summary: StatsSummary
      byFlow: ByFlowRow[]
      byApprover: ByApproverRow[]
    }>('/sys/flow/instance/stats', {
      params: {
        beginTime: dateRange.value?.[0],
        endTime: dateRange.value?.[1]
      }
    })
    summary.value = data.summary
    byFlow.value = data.byFlow
    byApprover.value = data.byApprover
  } finally {
    loading.value = false
  }
}

function handleReset(): void {
  dateRange.value = null
  loadData()
}

/** -1 = 无样本 */
function hours(v: number): string {
  return v < 0 ? '-' : v >= 24 ? `${(v / 24).toFixed(1)} 天` : `${v.toFixed(1)} 小时`
}

onMounted(loadData)
</script>

<template>
  <el-card v-loading="loading">
    <div class="toolbar">
      <el-date-picker
        v-model="dateRange"
        type="daterange"
        range-separator="-"
        start-placeholder="开始日期"
        end-placeholder="结束日期"
        value-format="YYYY-MM-DD"
        style="width: 260px"
      />
      <el-button type="primary" :icon="Search" @click="loadData">查询</el-button>
      <el-button :icon="Refresh" @click="handleReset">重置</el-button>
    </div>

    <!-- 汇总卡片 -->
    <el-row :gutter="12" class="cards">
      <el-col :span="4"><div class="stat-card"><div class="num">{{ summary?.total ?? 0 }}</div><div class="lbl">实例总数</div></div></el-col>
      <el-col :span="4"><div class="stat-card"><div class="num running">{{ summary?.running ?? 0 }}</div><div class="lbl">审批中</div></div></el-col>
      <el-col :span="4"><div class="stat-card"><div class="num ok">{{ summary?.approved ?? 0 }}</div><div class="lbl">已通过</div></div></el-col>
      <el-col :span="4"><div class="stat-card"><div class="num bad">{{ summary?.rejected ?? 0 }}</div><div class="lbl">已拒绝</div></div></el-col>
      <el-col :span="4"><div class="stat-card"><div class="num">{{ hours(summary?.avgApproveHours ?? -1) }}</div><div class="lbl">平均通过时长</div></div></el-col>
      <el-col :span="4"><div class="stat-card"><div class="num" :class="{ bad: (summary?.overduePending ?? 0) > 0 }">{{ summary?.overduePending ?? 0 }}</div><div class="lbl">超3天待办</div></div></el-col>
    </el-row>

    <el-tabs>
      <el-tab-pane label="按流程">
        <el-table :data="byFlow" border stripe size="small">
          <el-table-column prop="flowCode" label="流程编码" min-width="130" />
          <el-table-column prop="total" label="总数" width="80" align="center" />
          <el-table-column prop="running" label="审批中" width="80" align="center" />
          <el-table-column prop="approved" label="通过" width="80" align="center" />
          <el-table-column prop="rejected" label="拒绝" width="80" align="center" />
          <el-table-column label="平均通过时长" min-width="110" align="center">
            <template #default="{ row }">{{ hours((row as ByFlowRow).avgApproveHours) }}</template>
          </el-table-column>
        </el-table>
      </el-tab-pane>
      <el-tab-pane label="按审批人">
        <el-table :data="byApprover" border stripe size="small">
          <el-table-column prop="userName" label="审批人" min-width="110" />
          <el-table-column prop="handled" label="已处理" width="80" align="center" />
          <el-table-column prop="approved" label="同意" width="80" align="center" />
          <el-table-column prop="rejected" label="拒绝" width="80" align="center" />
          <el-table-column label="当前待办" width="90" align="center">
            <template #default="{ row }">
              <span :class="{ 'bad-num': (row as ByApproverRow).pending > 0 }">{{ (row as ByApproverRow).pending }}</span>
            </template>
          </el-table-column>
          <el-table-column label="平均处理时长" min-width="110" align="center">
            <template #default="{ row }">{{ hours((row as ByApproverRow).avgHandleHours) }}</template>
          </el-table-column>
        </el-table>
      </el-tab-pane>
    </el-tabs>
  </el-card>
</template>

<style scoped>
.toolbar {
  display: flex;
  gap: 8px;
  margin-bottom: 12px;
  flex-wrap: wrap;
}

.cards {
  margin-bottom: 16px;
}

.stat-card {
  text-align: center;
  padding: 14px 4px;
  background: var(--el-fill-color-light);
  border-radius: 6px;
}

.stat-card .num {
  font-size: 22px;
  font-weight: 700;
  color: var(--el-text-color-primary);
}

.stat-card .num.running { color: var(--el-color-primary); }
.stat-card .num.ok { color: var(--el-color-success); }
.stat-card .num.bad { color: var(--el-color-danger); }

.stat-card .lbl {
  margin-top: 4px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.bad-num {
  color: var(--el-color-danger);
  font-weight: 600;
}
</style>
