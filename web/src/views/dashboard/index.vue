<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { Clock, Document, Monitor, UserFilled } from '@element-plus/icons-vue'
import { initChart } from '@/utils/chart'
import type { EChartsOption, ECharts } from 'echarts'
import request from '@/api/request'

defineOptions({ name: 'DashboardView' })

interface DashboardStats {
  userCount: number
  roleCount: number
  onlineSessions: number
  todayLogins: number
  todayOperations: number
  noticeCount: number
  loginTrend: { date: string; success: number; failed: number }[]
  deptDistribution: { deptName: string; userCount: number }[]
}

const loading = ref(true)
const stats = ref<DashboardStats | null>(null)

const trendRef = ref<HTMLElement>()
const pieRef = ref<HTMLElement>()
let trendChart: ECharts | null = null
let pieChart: ECharts | null = null

async function loadData(): Promise<void> {
  loading.value = true
  try {
    stats.value = await request.get<never, DashboardStats>('/dashboard/stats')
    renderCharts()
  } finally {
    loading.value = false
  }
}

function renderCharts(): void {
  if (!stats.value) return

  // 登录趋势（折线）
  if (trendRef.value) {
    trendChart ??= initChart(trendRef.value)
    const option: EChartsOption = {
      tooltip: { trigger: 'axis' },
      legend: { data: ['登录成功', '登录失败'] },
      grid: { left: 40, right: 20, top: 40, bottom: 30 },
      xAxis: { type: 'category', data: stats.value.loginTrend.map((p) => p.date) },
      yAxis: { type: 'value', minInterval: 1 },
      series: [
        {
          name: '登录成功',
          type: 'line',
          smooth: true,
          data: stats.value.loginTrend.map((p) => p.success),
          itemStyle: { color: '#409eff' },
          areaStyle: { opacity: 0.15 }
        },
        {
          name: '登录失败',
          type: 'line',
          smooth: true,
          data: stats.value.loginTrend.map((p) => p.failed),
          itemStyle: { color: '#f56c6c' }
        }
      ]
    }
    trendChart.setOption(option, true)
  }

  // 部门用户分布（饼图）
  if (pieRef.value) {
    pieChart ??= initChart(pieRef.value)
    const option: EChartsOption = {
      tooltip: { trigger: 'item', formatter: '{b}: {c} 人 ({d}%)' },
      legend: { orient: 'vertical', right: 10, top: 'center' },
      series: [
        {
          name: '部门用户',
          type: 'pie',
          radius: ['40%', '70%'],
          center: ['40%', '50%'],
          avoidLabelOverlap: true,
          itemStyle: { borderRadius: 6, borderWidth: 2 },
          label: { show: false },
          data: stats.value.deptDistribution.map((d) => ({ name: d.deptName, value: d.userCount }))
        }
      ]
    }
    pieChart.setOption(option, true)
  }
}

function handleResize(): void {
  trendChart?.resize()
  pieChart?.resize()
}

onMounted(async () => {
  await loadData()
  window.addEventListener('resize', handleResize)
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', handleResize)
  trendChart?.dispose()
  pieChart?.dispose()
  trendChart = null
  pieChart = null
})
</script>

<template>
  <div v-loading="loading">
    <!-- 欢迎横幅 -->
    <el-card class="banner" shadow="never">
      <div>
        <h3 class="banner-title">欢迎使用 NetBase 管理系统</h3>
        <p class="banner-sub">三层架构 + RBAC 双维度权限 + 完整审计的企业级基础框架</p>
      </div>
    </el-card>

    <!-- 统计卡片 -->
    <el-row :gutter="16" class="stat-row">
      <el-col :span="6">
        <el-card shadow="hover">
          <div class="stat-card">
            <el-icon class="stat-icon blue"><UserFilled /></el-icon>
            <div>
              <div class="stat-value">{{ stats?.userCount ?? '-' }}</div>
              <div class="stat-label">用户总数</div>
            </div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card shadow="hover">
          <div class="stat-card">
            <el-icon class="stat-icon green"><Monitor /></el-icon>
            <div>
              <div class="stat-value">{{ stats?.onlineSessions ?? '-' }}</div>
              <div class="stat-label">在线会话</div>
            </div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card shadow="hover">
          <div class="stat-card">
            <el-icon class="stat-icon orange"><Clock /></el-icon>
            <div>
              <div class="stat-value">{{ stats?.todayLogins ?? '-' }}</div>
              <div class="stat-label">今日登录</div>
            </div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card shadow="hover">
          <div class="stat-card">
            <el-icon class="stat-icon red"><Document /></el-icon>
            <div>
              <div class="stat-value">{{ stats?.todayOperations ?? '-' }}</div>
              <div class="stat-label">今日操作</div>
            </div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <!-- 报表 -->
    <el-row :gutter="16" class="chart-row">
      <el-col :span="14">
        <el-card shadow="never">
          <template #header>
            <div class="chart-header">
              <span>近 7 天登录趋势</span>
              <el-tag size="small" type="info">成功 / 失败</el-tag>
            </div>
          </template>
          <div ref="trendRef" class="chart" />
        </el-card>
      </el-col>
      <el-col :span="10">
        <el-card shadow="never">
          <template #header>部门用户分布</template>
          <div ref="pieRef" class="chart" />
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<style scoped>
.banner {
  margin-bottom: 16px;
  background: linear-gradient(120deg, #409eff 0%, #2c6ecb 100%);
  border: none;
}

.banner :deep(.el-card__body) {
  color: #fff;
}

.banner-title {
  margin: 4px 0;
}

.banner-sub {
  margin: 0;
  opacity: 0.85;
  font-size: 13px;
}

.stat-row {
  margin-bottom: 16px;
}

.stat-card {
  display: flex;
  align-items: center;
  gap: 14px;
}

.stat-icon {
  font-size: 42px;
  padding: 12px;
  border-radius: 8px;
}

.stat-icon.blue {
  color: #409eff;
  background: rgba(64, 158, 255, 0.12);
}

.stat-icon.green {
  color: #67c23a;
  background: rgba(103, 194, 58, 0.12);
}

.stat-icon.orange {
  color: #e6a23c;
  background: rgba(230, 162, 60, 0.12);
}

.stat-icon.red {
  color: #f56c6c;
  background: rgba(245, 108, 108, 0.12);
}

.stat-value {
  font-size: 26px;
  font-weight: 700;
  line-height: 1.2;
}

.stat-label {
  font-size: 13px;
  color: #909399;
}

.chart-row {
  margin-bottom: 16px;
}

.chart {
  height: 300px;
}

.chart-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
</style>
