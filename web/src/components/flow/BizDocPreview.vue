<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { BIZ_DOC_STATUS, getExpenseDetail, getPurchaseDetail } from '@/api/biz/documents'
import { formatTime } from '@/utils/format'

/**
 * 业务单据预览（审批弹层"业务单据"页签）：
 * 按 businessTable + businessId 拉取单据详情并渲染。新单据类型接入审批流后，
 * 在 loaders 登记一行"表名 → 拉取与字段映射"即可在此展示。
 */
const props = defineProps<{ businessTable: string; businessId: string }>()

const loading = ref(false)
const fields = ref<Array<{ label: string; value: string }>>([])
const status = ref<number | null>(null)
const supported = ref(true)

const STATUS_LABEL: Record<number, string> = Object.fromEntries(
  Object.entries(BIZ_DOC_STATUS).map(([k, v]) => [Number(k), v.label])
)

const money = (n: number) => `¥${Number(n).toFixed(2)}`

const loaders: Record<string, (id: string) => Promise<{ status: number; fields: Array<{ label: string; value: string }> }>> = {
  biz_expense: async (id) => {
    const d = await getExpenseDetail(id)
    return {
      status: d.status,
      fields: [
        { label: '标题', value: d.title },
        { label: '金额', value: money(d.amount) },
        { label: '事由', value: d.reason || '-' },
        { label: '创建时间', value: formatTime(d.createTime) }
      ]
    }
  },
  biz_purchase_request: async (id) => {
    const d = await getPurchaseDetail(id)
    return {
      status: d.status,
      fields: [
        { label: '标题', value: d.title },
        { label: '采购物品', value: d.itemName },
        { label: '预算金额', value: money(d.amount) },
        { label: '申请事由', value: d.reason || '-' },
        { label: '创建时间', value: formatTime(d.createTime) }
      ]
    }
  }
}

async function load(): Promise<void> {
  fields.value = []
  status.value = null
  const loader = loaders[props.businessTable]
  if (!loader) {
    supported.value = false
    return
  }
  supported.value = true
  loading.value = true
  try {
    const result = await loader(props.businessId)
    fields.value = result.fields
    status.value = result.status
  } finally {
    loading.value = false
  }
}

watch(() => [props.businessTable, props.businessId], load)
onMounted(load)
</script>

<template>
  <div v-loading="loading" class="biz-doc-preview">
    <template v-if="supported">
      <div v-if="status !== null" class="status-row">
        <el-tag :type="BIZ_DOC_STATUS[status]?.type ?? 'info'" size="small">
          {{ STATUS_LABEL[status] ?? '未知' }}
        </el-tag>
      </div>
      <el-descriptions :column="1" border size="small">
        <el-descriptions-item v-for="f in fields" :key="f.label" :label="f.label">
          {{ f.value }}
        </el-descriptions-item>
      </el-descriptions>
    </template>
    <el-alert v-else type="info" :closable="false"
      :title="`单据类型 ${businessTable} 暂未接入详情展示`" description="可在 BizDocPreview 组件中登记该单据类型的字段映射。" />
  </div>
</template>

<style scoped>
.biz-doc-preview {
  min-height: 80px;
}

.status-row {
  margin-bottom: 10px;
}
</style>
