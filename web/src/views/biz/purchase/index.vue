<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Refresh, Search } from '@element-plus/icons-vue'
import type { PurchaseDoc } from '@/api/biz/documents'
import {
  BIZ_DOC_STATUS,
  createPurchase,
  deletePurchase,
  submitPurchase,
  updatePurchase
} from '@/api/biz/documents'
import { formatDateTime } from '@/utils/format'
import { usePageList } from '@/composables/usePageList'
import FlowTimeline from '@/components/flow/FlowTimeline.vue'
import { getFlowInstanceByBusiness } from '@/api/flow'

defineOptions({ name: 'BizPurchaseView' })

const {
  loading,
  firstLoading,
  list,
  total,
  query,
  loadData,
  handleSearch
} = usePageList<PurchaseDoc, { pageIndex: number; pageSize: number; keyword: string }>({
  url: '/biz/purchase/page',
  defaultQuery: { pageIndex: 1, pageSize: 10, keyword: '' }
})

function handleReset(): void {
  query.keyword = ''
  handleSearch()
}

// ---------- 新建 / 编辑 ----------
const dialogVisible = ref(false)
const saving = ref(false)
const editingId = ref<string | null>(null)
const formRef = ref()
const form = ref({ title: '', itemName: '', amount: 0, reason: '' })
const rules = {
  title: [{ required: true, message: '请输入申请标题', trigger: 'blur' }],
  itemName: [{ required: true, message: '请输入采购物品', trigger: 'blur' }],
  amount: [{ required: true, message: '请输入预算金额', trigger: 'blur' }]
}

function openCreate(): void {
  editingId.value = null
  form.value = { title: '', itemName: '', amount: 0, reason: '' }
  dialogVisible.value = true
}

function openEdit(row: PurchaseDoc): void {
  editingId.value = row.id
  form.value = { title: row.title, itemName: row.itemName, amount: row.amount, reason: row.reason ?? '' }
  dialogVisible.value = true
}

async function handleSave(): Promise<void> {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return
  saving.value = true
  try {
    if (editingId.value == null) {
      await createPurchase({ ...form.value })
      ElMessage.success('已保存草稿')
    } else {
      await updatePurchase(editingId.value, { ...form.value })
      ElMessage.success('已更新')
    }
    dialogVisible.value = false
    loadData()
  } finally {
    saving.value = false
  }
}

async function handleSubmit(row: PurchaseDoc): Promise<void> {
  await ElMessageBox.confirm(
    `提交「${row.title}」进入审批流？（金额 ≥ 1 万将路由至更高级别审批）`,
    '提示',
    { type: 'info' }
  )
  await submitPurchase(row.id)
  ElMessage.success('已提交审批')
  loadData()
}

async function handleDelete(row: PurchaseDoc): Promise<void> {
  await ElMessageBox.confirm(`确定删除「${row.title}」吗？`, '提示', { type: 'warning' })
  await deletePurchase(row.id)
  ElMessage.success('删除成功')
  loadData()
}

// ---------- 审批进度 ----------
const timelineVisible = ref(false)
const currentDoc = ref<PurchaseDoc | null>(null)
const currentInstanceId = ref('')

async function openTimeline(row: PurchaseDoc): Promise<void> {
  currentDoc.value = row
  currentInstanceId.value = ''
  timelineVisible.value = true
  const instance = await getFlowInstanceByBusiness('biz_purchase_request', row.id)
  currentInstanceId.value = instance?.id ?? ''
}

onMounted(loadData)
  import TableEmpty from '@/components/TableEmpty.vue'
  import TableSkeleton from '@/components/TableSkeleton.vue'
</script>

<template>
  <el-card>
    <div class="toolbar">
      <el-input
        v-model="query.keyword"
        placeholder="标题关键字"
        clearable
        style="width: 200px"
        :prefix-icon="Search"
        @keyup.enter="handleSearch"
      />
      <el-button type="primary" :icon="Search" @click="handleSearch">查询</el-button>
      <el-button :icon="Refresh" @click="handleReset">重置</el-button>
      <el-button v-permission="'biz:purchase:add'" type="primary" :icon="Plus" @click="openCreate">新建采购申请</el-button>
    </div>

    <TableSkeleton v-if="firstLoading" />
      <el-table v-else v-loading="loading" :data="list" border stripe>
      <el-table-column prop="title" label="标题" min-width="160" show-overflow-tooltip />
      <el-table-column prop="itemName" label="采购物品" min-width="120" show-overflow-tooltip />
      <el-table-column prop="amount" label="预算金额" width="120" align="right">
        <template #default="{ row }">¥{{ (row as PurchaseDoc).amount.toFixed(2) }}</template>
      </el-table-column>
      <el-table-column label="状态" width="90" align="center">
        <template #default="{ row }">
          <el-tag :type="BIZ_DOC_STATUS[(row as PurchaseDoc).status]?.type ?? 'info'" size="small">
            {{ BIZ_DOC_STATUS[(row as PurchaseDoc).status]?.label }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="createTime" label="创建时间" width="165" :formatter="formatDateTime" />
      <el-table-column label="操作" width="220" align="center">
        <template #default="{ row }">
          <el-button
            v-if="(row as PurchaseDoc).status === 0"
            v-permission="'biz:purchase:edit'"
            link
            type="primary"
            @click="openEdit(row as PurchaseDoc)"
          >编辑</el-button>
          <el-button
            v-if="(row as PurchaseDoc).status === 0 || (row as PurchaseDoc).status === 3"
            v-permission="'biz:purchase:edit'"
            link
            type="success"
            @click="handleSubmit(row as PurchaseDoc)"
          >提交审批</el-button>
          <el-button link type="primary" @click="openTimeline(row as PurchaseDoc)">审批进度</el-button>
          <el-button
            v-if="(row as PurchaseDoc).status === 0"
            v-permission="'biz:purchase:delete'"
            link
            type="danger"
            @click="handleDelete(row as PurchaseDoc)"
          >删除</el-button>
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
      layout="total, sizes, prev, pager, next, jumper"
      :page-sizes="[10, 20, 50, 100]"
        :total="total"
      @size-change="() => { query.pageIndex = 1; loadData() }"
          @current-change="loadData"
    />

    <el-dialog v-model="dialogVisible" :title="editingId == null ? '新建采购申请' : '编辑采购申请'" width="480px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="80px">
        <el-form-item label="标题" prop="title">
          <el-input v-model="form.title" maxlength="100" />
        </el-form-item>
        <el-form-item label="采购物品" prop="itemName">
          <el-input v-model="form.itemName" maxlength="100" />
        </el-form-item>
        <el-form-item label="预算金额" prop="amount">
          <el-input-number v-model="form.amount" :min="0.01" :precision="2" style="width: 100%" />
        </el-form-item>
        <el-form-item label="事由">
          <el-input v-model="form.reason" type="textarea" :rows="3" maxlength="500" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">保存草稿</el-button>
      </template>
    </el-dialog>

    <el-drawer v-model="timelineVisible" title="审批进度" size="460px">
      <div v-if="currentDoc" class="doc-meta">
        <div>{{ currentDoc.title }} · {{ currentDoc.itemName }} · ¥{{ currentDoc.amount.toFixed(2) }}</div>
        <div class="reason">{{ currentDoc.reason }}</div>
      </div>
      <FlowTimeline v-if="currentInstanceId" :instance-id="currentInstanceId" />
      <el-empty v-else description="尚未提交审批" :image-size="60" />
    </el-drawer>
  </el-card>
</template>

<style scoped>
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

.doc-meta {
  margin-bottom: 12px;
}

.doc-meta .reason {
  margin-top: 4px;
  color: var(--el-text-color-secondary);
  font-size: 13px;
}
</style>
