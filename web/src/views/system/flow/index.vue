<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Refresh, Search } from '@element-plus/icons-vue'
import type { FlowDefinition } from '@/api/flow'
import { createFlowDef, deleteFlowDef, disableFlowDef, enableFlowDef, getFlowDefPage, updateFlowDef } from '@/api/flow'
import type { FlowBinding } from '@/api/flow'
import { deleteFlowBinding, getFlowBindings, saveFlowBinding } from '@/api/flow'
import { deleteFlowCategory, getFlowCategories, renameFlowCategory } from '@/api/flow'
import type { FlowCategory } from '@/api/flow'
import { formatDateTime } from '@/utils/format'
import { usePageList } from '@/composables/usePageList'
import FlowDesigner from '@/components/flow/FlowDesigner.vue'

defineOptions({ name: 'SystemFlowView' })

const {
  loading,
  firstLoading,
  list,
  total,
  query,
  loadData,
  handleSearch
} = usePageList<FlowDefinition, { pageIndex: number; pageSize: number; keyword: string; category?: string }>({
  url: '/sys/flow/def/page',
  defaultQuery: { pageIndex: 1, pageSize: 10, keyword: '', category: undefined }
})

function handleReset(): void {
  query.keyword = ''
  query.category = undefined
  handleSearch()
}

// ---------- 新建 / 编辑（抽屉 + 设计器） ----------
const drawerVisible = ref(false)
const saving = ref(false)
const designerRef = ref<InstanceType<typeof FlowDesigner> | null>(null)
const editingId = ref<string | null>(null)
const form = ref({
  flowCode: '',
  category: '',
  flowName: '',
  nodeJson: '',
  enabled: false,
  remark: ''
})
/** 分类选项（从现有流程收集，可输入新分类） */
const categoryOptions = computed(() =>
  [...new Set(list.value.map((d) => d.category).filter(Boolean))] as string[]
)

function openCreate(): void {
  editingId.value = null
  form.value = { flowCode: '', category: '', flowName: '', nodeJson: '', enabled: false, remark: '' }
  drawerVisible.value = true
}

async function openEdit(row: FlowDefinition): Promise<void> {
  editingId.value = row.id
  form.value = {
    flowCode: row.flowCode,
    category: row.category ?? '',
    flowName: row.flowName,
    nodeJson: row.nodeJson,
    enabled: row.status === 1,
    remark: row.remark ?? ''
  }
  drawerVisible.value = true
}

async function handleSave(): Promise<void> {
  if (!form.value.flowName) {
    ElMessage.warning('请填写流程名称')
    return
  }
  const invalid = designerRef.value?.validate()
  if (invalid) {
    ElMessage.warning(invalid)
    return
  }
  saving.value = true
  try {
    if (editingId.value == null) {
      await createFlowDef({ ...form.value })
      ElMessage.success('创建成功（新建后请启用生效）')
    } else {
      await updateFlowDef(editingId.value, { ...form.value })
      ElMessage.success('保存成功')
    }
    drawerVisible.value = false
    loadData()
  } finally {
    saving.value = false
  }
}

async function handleEnable(row: FlowDefinition): Promise<void> {
  await enableFlowDef(row.id)
  ElMessage.success('已启用（同编码其他版本已自动停用）')
  loadData()
}

async function handleDisable(row: FlowDefinition): Promise<void> {
  await disableFlowDef(row.id)
  ElMessage.success('已停用')
  loadData()
}

async function handleDelete(row: FlowDefinition): Promise<void> {
  await ElMessageBox.confirm(`确定删除流程「${row.flowName}」v${row.flowVersion} 吗？`, '提示', { type: 'warning' })
  await deleteFlowDef(row.id)
  ElMessage.success('删除成功')
  loadData()
}

// ---------- 单据绑定（运行时换流程/停用审批） ----------
const bindingVisible = ref(false)
const bindings = ref<FlowBinding[]>([])
const bindingLoading = ref(false)
const enabledFlowOptions = ref<Array<{ code: string; label: string }>>([])
const bindingForm = ref({ businessTable: '', flowCode: '', remark: '' })

async function openBindings(): Promise<void> {
  bindingVisible.value = true
  bindingLoading.value = true
  try {
    const [list, defs] = await Promise.all([
      getFlowBindings(),
      getFlowDefPage({ pageIndex: 1, pageSize: 100 })
    ])
    bindings.value = list
    enabledFlowOptions.value = defs.items
      .filter((d) => d.status === 1)
      .map((d) => ({ code: d.flowCode, label: `${d.flowCode} · ${d.flowName}` }))
  } finally {
    bindingLoading.value = false
  }
}

/** 已知业务表建议项（新单据类型可直接输入表名） */
const bizTableSuggestions = ['biz_expense', 'biz_purchase_request']

async function handleSaveBinding(row?: FlowBinding): Promise<void> {
  const businessTable = row?.businessTable ?? bindingForm.value.businessTable.trim()
  if (!businessTable) {
    ElMessage.warning('请填写业务表名')
    return
  }
  const flowCode = row ? (row.flowCode ?? '') : bindingForm.value.flowCode
  const remark = row?.remark ?? bindingForm.value.remark
  await saveFlowBinding({ businessTable, flowCode: flowCode || undefined, remark: remark || undefined })
  ElMessage.success(flowCode ? `已绑定：${businessTable} → ${flowCode}` : `已解绑：${businessTable}（提交审批将被拒绝）`)
  await openBindings()
  if (!row) {
    bindingForm.value = { businessTable: '', flowCode: '', remark: '' }
  }
}

async function handleDeleteBinding(row: FlowBinding): Promise<void> {
  await ElMessageBox.confirm(
    `删除 ${row.businessTable} 的绑定后，提交将回退到业务代码默认流程。确定删除？`,
    '提示',
    { type: 'warning' }
  )
  await deleteFlowBinding(row.id)
  ElMessage.success('已删除')
  openBindings()
}

// ---------- 分类管理（重命名自动合并；删除仅限未被流程使用） ----------
const categoryVisible = ref(false)
const categories = ref<FlowCategory[]>([])
const categoryLoading = ref(false)

async function openCategoryManager(): Promise<void> {
  categoryVisible.value = true
  await reloadCategories()
}

async function reloadCategories(): Promise<void> {
  categoryLoading.value = true
  try {
    categories.value = await getFlowCategories()
  } finally {
    categoryLoading.value = false
  }
}

async function handleRenameCategory(row: FlowCategory): Promise<void> {
  const { value } = await ElMessageBox.prompt(`将「${row.name}」（${row.count} 个流程）重命名为：`, '重命名分类', {
    inputValue: row.name,
    inputPattern: /^\S{1,50}$/,
    inputErrorMessage: '分类名不能为空且不超过 50 字符'
  })
  await renameFlowCategory(row.name, value.trim())
  ElMessage.success(value.trim() !== row.name ? '已重命名' : '名称未变化')
  await reloadCategories()
  loadData()
}

async function handleDeleteCategory(row: FlowCategory): Promise<void> {
  await ElMessageBox.confirm(
    row.count > 0
      ? `「${row.name}」下还有 ${row.count} 个流程，无法删除。如需移除分类请先在流程上移出。`
      : `确定删除分类「${row.name}」吗？`,
    '删除分类',
    { type: 'warning', showCancelButton: row.count === 0 }
  )
  await deleteFlowCategory(row.name)
  ElMessage.success('已删除')
  await reloadCategories()
  loadData()
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
        placeholder="编码/名称"
        clearable
        style="width: 200px"
        :prefix-icon="Search"
        @keyup.enter="handleSearch"
      />
      <el-select v-model="query.category" placeholder="分类" clearable style="width: 130px">
        <el-option v-for="c in categoryOptions" :key="c" :label="c" :value="c" />
      </el-select>
      <el-button type="primary" :icon="Search" @click="handleSearch">查询</el-button>
      <el-button :icon="Refresh" @click="handleReset">重置</el-button>
      <el-button v-permission="'sys:flow:add'" type="primary" :icon="Plus" @click="openCreate">新建流程</el-button>
      <el-button v-permission="'sys:flow:list'" type="warning" plain @click="openBindings">单据绑定</el-button>
      <el-button v-permission="'sys:flow:list'" plain @click="openCategoryManager">分类管理</el-button>
    </div>

    <TableSkeleton v-if="firstLoading" />
      <el-table v-else v-loading="loading" :data="list" border stripe>
      <el-table-column prop="flowCode" label="编号" width="80" align="center" />
      <el-table-column prop="category" label="分类" width="100">
        <template #default="{ row }">
          <el-tag v-if="(row as FlowDefinition).category" size="small" type="info">
            {{ (row as FlowDefinition).category }}
          </el-tag>
          <span v-else>-</span>
        </template>
      </el-table-column>
      <el-table-column prop="flowName" label="流程名称" min-width="160" />
      <el-table-column prop="version" label="版本" width="70" align="center">
        <template #default="{ row }">v{{ (row as FlowDefinition).flowVersion }}</template>
      </el-table-column>
      <el-table-column label="状态" width="90" align="center">
        <template #default="{ row }">
          <el-tag :type="(row as FlowDefinition).status === 1 ? 'success' : 'info'" size="small">
            {{ (row as FlowDefinition).status === 1 ? '启用' : '停用' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="remark" label="备注" min-width="140" show-overflow-tooltip />
      <el-table-column prop="createTime" label="创建时间" width="165" :formatter="formatDateTime" />
      <el-table-column label="操作" width="230" align="center">
        <template #default="{ row }">
          <el-button v-permission="'sys:flow:list'" link type="primary" @click="openEdit(row as FlowDefinition)">设计</el-button>
          <el-button
            v-if="(row as FlowDefinition).status !== 1"
            v-permission="'sys:flow:edit'"
            link
            type="success"
            @click="handleEnable(row as FlowDefinition)"
          >启用</el-button>
          <el-button
            v-else
            v-permission="'sys:flow:edit'"
            link
            type="warning"
            @click="handleDisable(row as FlowDefinition)"
          >停用</el-button>
          <el-button
            v-if="(row as FlowDefinition).status !== 1"
            v-permission="'sys:flow:delete'"
            link
            type="danger"
            @click="handleDelete(row as FlowDefinition)"
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
      layout="total, prev, pager, next"
      :total="total"
      @current-change="loadData"
    />

    <el-dialog v-model="bindingVisible" title="单据绑定（业务表 → 审批流）" width="720px">
      <div v-loading="bindingLoading">
        <el-alert type="info" :closable="false" class="binding-tip"
          title="绑定优先于代码默认：有绑定记录以绑定为准；流程编码留空 = 该单据不走审批（提交时拒绝）；删除绑定 = 回退代码默认流程。" />
        <el-table :data="bindings" border size="small">
          <el-table-column prop="businessTable" label="业务表" min-width="170" />
          <el-table-column prop="remark" label="说明" min-width="90" />
          <el-table-column label="绑定流程" min-width="200">
            <template #default="{ row }">
              <el-select
                :model-value="(row as FlowBinding).flowCode ?? ''"
                clearable
                placeholder="留空 = 不走审批"
                size="small"
                style="width: 100%"
                @change="(v: string) => { (row as FlowBinding).flowCode = v || undefined; handleSaveBinding(row as FlowBinding) }"
              >
                <el-option v-for="o in enabledFlowOptions" :key="o.code" :label="o.label" :value="o.code" />
              </el-select>
            </template>
          </el-table-column>
          <el-table-column label="操作" width="70" align="center">
            <template #default="{ row }">
              <el-button link type="danger" size="small" @click="handleDeleteBinding(row as FlowBinding)">删除</el-button>
            </template>
          </el-table-column>
        <template #empty>
          <TableEmpty />
        </template>
        </el-table>

        <el-divider content-position="left">新增绑定</el-divider>
        <div class="binding-add">
          <el-select
            v-model="bindingForm.businessTable"
            filterable
            allow-create
            default-first-option
            placeholder="业务表名（可选择或输入）"
            style="width: 220px"
          >
            <el-option v-for="t in bizTableSuggestions" :key="t" :label="t" :value="t" />
          </el-select>
          <el-select v-model="bindingForm.flowCode" clearable placeholder="流程（留空=不走审批）" style="width: 220px">
            <el-option v-for="o in enabledFlowOptions" :key="o.code" :label="o.label" :value="o.code" />
          </el-select>
          <el-input v-model="bindingForm.remark" placeholder="说明" style="width: 140px" />
          <el-button type="primary" @click="handleSaveBinding()">保存</el-button>
        </div>
      </div>
    </el-dialog>

    <el-dialog v-model="categoryVisible" title="流程分类管理" width="520px">
      <div v-loading="categoryLoading">
        <el-alert type="info" :closable="false" class="binding-tip"
          title="重命名会自动合并到同名分类；分类下还有流程时不可删除（先在流程上移出分类）。新分类在新建/编辑流程时输入。" />
        <el-table :data="categories" border size="small">
          <el-table-column prop="name" label="分类" min-width="140" />
          <el-table-column prop="count" label="流程数" width="80" align="center" />
          <el-table-column label="操作" width="130" align="center">
            <template #default="{ row }">
              <el-button link type="primary" size="small" @click="handleRenameCategory(row as FlowCategory)">重命名</el-button>
              <el-button
                link
                type="danger"
                size="small"
                :disabled="(row as FlowCategory).count > 0"
                @click="handleDeleteCategory(row as FlowCategory)"
              >删除</el-button>
            </template>
          </el-table-column>
        <template #empty>
          <TableEmpty />
        </template>
        </el-table>
      </div>
    </el-dialog>

    <el-drawer v-model="drawerVisible" :title="editingId == null ? '新建流程' : '设计流程'" size="520px">
      <el-form label-width="90px">
        <el-form-item label="流程编号">
          <el-input v-if="editingId != null" v-model="form.flowCode" disabled />
          <el-text v-else type="info">保存后由系统自动生成</el-text>
        </el-form-item>
        <el-form-item label="分类">
          <el-select
            v-model="form.category"
            filterable
            allow-create
            default-first-option
            clearable
            placeholder="如 财务类/采购类（可输入新分类）"
            style="width: 100%"
          >
            <el-option v-for="c in categoryOptions" :key="c" :label="c" :value="c" />
          </el-select>
        </el-form-item>
        <el-form-item label="流程名称" required>
          <el-input v-model="form.flowName" placeholder="如 报销审批流" maxlength="100" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="form.remark" type="textarea" :rows="2" maxlength="200" />
        </el-form-item>
        <el-form-item label="流程设计">
          <FlowDesigner ref="designerRef" v-model="form.nodeJson" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="drawerVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">保存</el-button>
      </template>
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

.binding-tip {
  margin-bottom: 10px;
}

.binding-add {
  display: flex;
  gap: 8px;
  align-items: center;
  flex-wrap: wrap;
}

.pagination {
  margin-top: 12px;
  justify-content: flex-end;
}
</style>
