<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { CircleCheckFilled, Clock, RemoveFilled, WarningFilled } from '@element-plus/icons-vue'
import { getFlowInstanceDetail } from '@/api/flow'
import type { FlowInstanceDetail, FlowTimelineItem } from '@/api/flow'
import { formatTime } from '@/utils/format'

/** 审批流时间线：传入实例ID自动拉取详情并渲染钉钉式竖向流转记录 */
const props = defineProps<{ instanceId: string | number }>()

const loading = ref(false)
const detail = ref<FlowInstanceDetail | null>(null)

const ACTION_META: Record<string, { label: string; icon: typeof Clock; color: string }> = {
  submit: { label: '提交审批', icon: Clock, color: '#409eff' },
  approve: { label: '同意', icon: CircleCheckFilled, color: '#67c23a' },
  reject: { label: '拒绝', icon: RemoveFilled, color: '#f56c6c' },
  transfer: { label: '转办', icon: Clock, color: '#e6a23c' },
  addsign: { label: '加签', icon: Clock, color: '#e6a23c' },
  withdraw: { label: '撤回', icon: WarningFilled, color: '#909399' },
  cc: { label: '抄送', icon: Clock, color: '#909399' },
  auto: { label: '自动通过', icon: CircleCheckFilled, color: '#67c23a' },
  void: { label: '作废', icon: WarningFilled, color: '#909399' }
}

function metaOf(item: FlowTimelineItem) {
  return ACTION_META[item.action] ?? { label: item.action, icon: Clock, color: '#909399' }
}

async function load(): Promise<void> {
  if (!props.instanceId) return
  loading.value = true
  try {
    detail.value = await getFlowInstanceDetail(props.instanceId)
  } finally {
    loading.value = false
  }
}

watch(() => props.instanceId, load)
onMounted(load)
</script>

<template>
  <div v-loading="loading" class="flow-timeline">
    <template v-if="detail">
      <div class="instance-head">
        <span class="summary">{{ detail.instance.summary }}</span>
        <el-tag
          :type="detail.instance.status === 2 ? 'success' : detail.instance.status === 3 ? 'danger' : 'primary'"
          size="small"
        >
          {{ detail.instance.status === 1 ? '审批中' : detail.instance.status === 2 ? '已通过' : detail.instance.status === 3 ? '已拒绝' : detail.instance.status === 4 ? '已撤回' : '已作废' }}
        </el-tag>
      </div>

      <el-timeline class="timeline">
        <el-timeline-item
          v-for="(item, index) in detail.timeline"
          :key="index"
          :timestamp="formatTime(item.time)"
          :color="metaOf(item).color"
          placement="top"
        >
          <div class="item-head">
            <el-icon :color="metaOf(item).color" class="item-icon"><component :is="metaOf(item).icon" /></el-icon>
            <span class="node-name">{{ item.nodeName ? `${item.nodeName} · ` : '' }}{{ metaOf(item).label }}</span>
            <el-avatar :size="20" :src="item.avatar || undefined" class="operator-avatar">
              {{ item.operatorName.charAt(0).toUpperCase() }}
            </el-avatar>
            <span class="operator">{{ item.operatorName }}</span>
          </div>
          <div v-if="item.comment" class="comment">{{ item.comment }}</div>
        </el-timeline-item>
      </el-timeline>
    </template>
    <el-empty v-else-if="!loading" description="暂无审批记录" :image-size="60" />
  </div>
</template>

<style scoped>
.flow-timeline {
  padding: 4px 4px 4px 8px;
}

.instance-head {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
}

.summary {
  font-weight: 600;
}

.timeline {
  padding-left: 4px;
}

.item-head {
  display: flex;
  align-items: center;
  gap: 6px;
}

.item-icon {
  font-size: 16px;
}

.node-name {
  font-weight: 500;
}

.operator {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.comment {
  margin-top: 4px;
  padding: 6px 10px;
  background: var(--el-fill-color-light);
  border-radius: 4px;
  color: var(--el-text-color-regular);
  font-size: 13px;
}
</style>
