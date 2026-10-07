<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Delete, Download, Plus, Refresh, Search, View } from '@element-plus/icons-vue'
import {
  deleteGenTable,
  downloadGen,
  getDbColumns,
  getDbTables,
  getGenDetail,
  importGenTable,
  previewGen,
  saveGenTable,
  type GenColumn,
  type GenPreviewFile,
  type GenSaveRequest,
  type GenTable,
  type GenTableBrief
} from '@/api/system/gen'
import { usePageList } from '@/composables/usePageList'

defineOptions({ name: 'SystemGenView' })

const activeTab = ref('list')
function handleTabChange(_name: string | number): void {} // Tab 切换无额外逻辑（列表懒加载由 usePageList 承担）

const UI_TYPES = [
  { value: 'input', label: '输入框' },
  { value: 'number', label: '数字' },
  { value: 'date', label: '日期' },
  { value: 'datetime', label: '日期时间' },
  { value: 'select', label: '下拉' },
  { value: 'textarea', label: '文本域' },
  { value: 'switch', label: '开关' }
]
// ---------- Tab1：配置列表 ----------
const { loading, firstLoading, list, total, query, loadData, handleSearch } = usePageList<GenTable, { pageIndex: number; pageSize: number; keyword: string }>({
  url: '/sys/gen/page',
  defaultQuery: { pageIndex: 1, pageSize: 10, keyword: '' }
})

async function handleDelete(row: GenTable): Promise<void> {
  await ElMessageBox.confirm(`确定删除「${row.tableName}」的生成配置吗？`, '提示', { type: 'warning' })
  await deleteGenTable(row.id)
  ElMessage.success('删除成功')
  loadData()
}

async function handleDownload(row: GenTable): Promise<void> {
  const blob = await downloadGen(row.id)
  const a = document.createElement('a')
  a.href = URL.createObjectURL(blob)
  a.download = `${row.tableName}-gen.zip`
  a.click()
  URL.revokeObjectURL(a.href)
}

// ---------- Tab2：新建/编辑向导 ----------
const wizardVisible = ref(false)
const wizardMode = ref<'create' | 'edit'>('create')
const step = ref(0)

const form = reactive({
  id: undefined as string | undefined,
  tableName: '',
  tableComment: '',
  moduleName: '',
  functionName: '',
  entityName: '',
  author: 'system',
  dataScope: false,
  flowDoc: false,
  sourceType: 'manual'
})

const columns = ref<GenColumn[]>([])
const subTables = ref<Array<{ tableName: string; tableComment: string; fkColumn: string; entityName: string; columns: GenColumn[] }>>([])

const formRules = {
  tableName: [
    { required: true, message: '请输入表名（biz_xxx）', trigger: 'blur' },
    { pattern: /^biz_[a-z][a-z0-9_]*$/, message: '业务表名须为 biz_ 小写开头', trigger: 'blur' }
  ],
  moduleName: [
    { required: true, message: '请输入模块名', trigger: 'blur' },
    { pattern: /^[a-z][a-z0-9_]*$/, message: '模块名仅小写字母数字下划线', trigger: 'blur' }
  ],
  functionName: [{ required: true, message: '请输入功能名', trigger: 'blur' }],
  entityName: [
    { required: true, message: '请输入实体名', trigger: 'blur' },
    { pattern: /^[A-Z][A-Za-z0-9]*$/, message: '实体名须 PascalCase（如 Expense）', trigger: 'blur' }
  ]
}

// 库表选择
const dbTables = ref<GenTableBrief[]>([])
const dbTableKeyword = ref('')
const importing = ref(false)

async function openWizard(mode: 'create' | 'edit', row?: GenTable): Promise<void> {
  wizardMode.value = mode
  step.value = 0
  columns.value = []
  subTables.value = []
  if (mode === 'edit' && row) {
    const detail = await getGenDetail(row.id)
    Object.assign(form, {
      id: detail.table.id,
      tableName: detail.table.tableName,
      tableComment: detail.table.tableComment,
      moduleName: detail.table.moduleName,
      functionName: detail.table.functionName,
      entityName: detail.table.entityName,
      author: detail.table.author,
      dataScope: detail.table.dataScope,
      flowDoc: detail.table.flowDoc,
      sourceType: detail.table.sourceType
    })
    columns.value = detail.columns.filter((c) => !c.subTableName)
    for (const sub of detail.table.subTables ?? []) {
      subTables.value.push({
        tableName: sub.tableName,
        tableComment: sub.tableComment,
        fkColumn: sub.fkColumn,
        entityName: sub.entityName,
        columns: (detail.columns.filter((c) => c.subTableName === sub.tableName) as GenColumn[]) ?? []
      })
    }
  } else {
    Object.assign(form, {
      id: undefined,
      tableName: '',
      tableComment: '',
      moduleName: '',
      functionName: '',
      entityName: '',
      author: 'system',
      dataScope: false,
      flowDoc: false,
      sourceType: 'manual'
    })
  }
  wizardVisible.value = true
}

async function loadDbTables(): Promise<void> {
  dbTables.value = await getDbTables()
}

async function importTable(row: GenTableBrief): Promise<void> {
  // 从表名推导默认值：biz_expense → module=expense, entity=Expense
  const suffix = row.tableName.replace(/^biz_/, '')
  const moduleName = suffix.split('_')[0]
  const entityName = suffix.split('_').map((p) => p.charAt(0).toUpperCase() + p.slice(1)).join('')
  await importGenTable({
    tableName: row.tableName,
    moduleName,
    functionName: row.tableComment || row.tableName,
    entityName
  })
  ElMessage.success(`已导入 ${row.tableName}，可在配置列表继续编辑字段`)
  wizardVisible.value = false
  loadData()
}

async function loadDbColumns(tableName: string): Promise<GenColumn[]> {
  const metas = await getDbColumns(tableName)
  return metas.map((m, i) => ({
    columnName: m.columnName,
    propertyName: m.columnName.split('_').map((p) => p.charAt(0).toUpperCase() + p.slice(1)).join(''),
    columnType: m.dataType.split('(')[0],
    cSharpType: m.cSharpType,
    displayName: m.columnComment || m.columnName,
    columnComment: m.columnComment,
    length: m.length,
    isPk: m.isPk,
    isRequired: m.isRequired && !m.isPk,
    isList: !m.isPk && !['createtime', 'createby', 'updatetime', 'updateby'].includes(m.columnName),
    isQuery: ['title', 'name', 'user_name', 'dict_name'].includes(m.columnName),
    isForm: !m.isPk,
    uiType: m.cSharpType === 'DateTime' ? 'datetime' : m.cSharpType === 'decimal' ? 'number' : m.cSharpType === 'bool' ? 'switch' : m.cSharpType === 'string' && m.length > 200 ? 'textarea' : 'input',
    sort: i,
    subTableName: undefined
  }))
}

/** 手工新建：按表名拉库列（表未建则报错，改手工添加字段） */
async function initColumnsFromDb(): Promise<void> {
  try {
    columns.value = await loadDbColumns(form.tableName)
    ElMessage.success(`已读取 ${columns.value.length} 列元数据`)
  } catch {
    ElMessage.warning('库中无此表（或尚未建表），请手工添加字段')
  }
}

function addColumn(): void {
  columns.value.push({
    columnName: '',
    propertyName: '',
    columnType: 'varchar',
    cSharpType: 'string',
    displayName: '',
    columnComment: '',
    length: 100,
    isPk: false,
    isRequired: false,
    isList: true,
    isQuery: false,
    isForm: true,
    uiType: 'input',
    sort: columns.value.length
  })
}

function removeColumn(index: number): void {
  columns.value.splice(index, 1)
}

function addSubTable(): void {
  subTables.value.push({ tableName: `biz_${form.moduleName}_${subTables.value.length + 1}_item`, tableComment: '', fkColumn: '', entityName: form.entityName + 'Item', columns: [] })
}

function removeSubTable(index: number): void {
  subTables.value.splice(index, 1)
}

async function loadSubColumns(sub: { tableName: string; tableComment: string; fkColumn: string; entityName: string; columns: GenColumn[] }): Promise<void> {
  try {
    sub.columns = await loadDbColumns(sub.tableName)
    ElMessage.success(`已读取 ${sub.tableName} 共 ${sub.columns.length} 列`)
  } catch {
    ElMessage.warning(`库中无表 ${sub.tableName}，请手工添加字段`)
  }
}

function addSubColumn(sub: { columns: GenColumn[] }): void {
  sub.columns.push({
    columnName: '',
    propertyName: '',
    columnType: 'varchar',
    cSharpType: 'string',
    displayName: '',
    columnComment: '',
    length: 100,
    isPk: false,
    isRequired: false,
    isList: true,
    isQuery: false,
    isForm: true,
    uiType: 'input',
    sort: sub.columns.length,
    subTableName: ''
  })
}

function removeSubColumn(sub: { columns: GenColumn[] }, index: number): void {
  sub.columns.splice(index, 1)
}

// ---------- 预览 / 保存 / 下载 ----------
const previewVisible = ref(false)
const previewFiles = ref<GenPreviewFile[]>([])
const previewActive = ref('')

async function handlePreview(): Promise<void> {
  const id = await doSave(true)
  previewFiles.value = await previewGen(id)
  previewActive.value = previewFiles.value[0]?.path ?? ''
  previewVisible.value = true
}

async function handleSaveAndClose(): Promise<void> {
  await doSave(false)
  wizardVisible.value = false
  loadData()
}

async function doSave(silent: boolean): Promise<string> {
  const request: GenSaveRequest = {
    id: form.id,
    tableName: form.tableName,
    tableComment: form.tableComment,
    moduleName: form.moduleName,
    functionName: form.functionName,
    entityName: form.entityName,
    author: form.author,
    dataScope: form.dataScope,
    flowDoc: form.flowDoc,
    sourceType: form.sourceType,
    subTables: subTables.value.length > 0 ? subTables.value : undefined,
    columns: columns.value
  }
  const id = await saveGenTable(request)
  if (!silent) {
    ElMessage.success('保存成功')
  }
  return id
}

onMounted(() => {
  loadData()
  loadDbTables()
})
  import TableEmpty from '@/components/TableEmpty.vue'
  import TableSkeleton from '@/components/TableSkeleton.vue'
</script>

<template>
  <el-card>
    <el-tabs v-model="activeTab" @tab-change="handleTabChange">
      <!-- Tab 1：生成配置列表 -->
      <el-tab-pane name="list" label="生成配置">
        <div class="toolbar">
          <el-input
            v-model="query.keyword"
            placeholder="表名/功能名"
            clearable
            style="width: 200px"
            :prefix-icon="Search"
            @keyup.enter="handleSearch"
          />
          <el-button type="primary" :icon="Search" @click="handleSearch">查询</el-button>
          <el-button :icon="Refresh" @click="handleSearch">刷新</el-button>
        </div>

        <TableSkeleton v-if="firstLoading" />
      <el-table v-else v-loading="loading" :data="list" border stripe>
          <el-table-column prop="tableName" label="表名" min-width="160" />
          <el-table-column prop="tableComment" label="功能描述" min-width="140" />
          <el-table-column prop="moduleName" label="模块" width="110" />
          <el-table-column prop="functionName" label="功能名" min-width="120" />
          <el-table-column label="特性" width="140" align="center">
            <template #default="{ row }">
              <el-tag v-if="(row as GenTable).flowDoc" size="small" type="warning">审批流</el-tag>
              <el-tag v-if="(row as GenTable).dataScope" size="small" type="info">数据权限</el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="createTime" label="配置时间" width="165" />
          <el-table-column label="操作" width="220" align="center">
            <template #default="{ row }">
              <el-button link type="primary" :icon="View" @click="openWizard('edit', row as GenTable)">编辑</el-button>
              <el-button link type="primary" :icon="Download" @click="handleDownload(row as GenTable)">下载</el-button>
              <el-button v-permission="'sys:gentable:delete'" link type="danger" :icon="Delete" @click="handleDelete(row as GenTable)">删除</el-button>
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
      </el-tab-pane>

      <!-- Tab 2：新建生成向导 -->
      <el-tab-pane name="wizard" label="新建生成">
        <el-alert type="info" :closable="false" style="margin-bottom: 12px"
          title="从库导入：选表自动读列元数据；手工新建：填表名后手工维护字段（可先配字段再生成建表 SQL）。主子表在 Step3 配置。" />

        <div class="toolbar">
          <el-input v-model="dbTableKeyword" placeholder="过滤库表名" clearable style="width: 200px" :prefix-icon="Search" />
          <el-button :icon="Refresh" @click="loadDbTables">刷新库表</el-button>
        </div>
        <el-table :data="dbTables.filter((t) => !dbTableKeyword || t.tableName.includes(dbTableKeyword))" border stripe max-height="320">
          <el-table-column prop="tableName" label="表名" min-width="200" />
          <el-table-column prop="tableComment" label="备注" min-width="160" />
          <el-table-column label="操作" width="120" align="center">
            <template #default="{ row }">
              <el-button v-permission="'sys:gentable:add'" link type="primary" :loading="importing" @click="importTable(row as GenTableBrief)">导入</el-button>
            </template>
          </el-table-column>
        <template #empty>
          <TableEmpty />
        </template>
        </el-table>
        <div style="margin-top: 12px">
          <el-button v-permission="'sys:gentable:add'" type="primary" :icon="Plus" @click="openWizard('create')">手工新建配置</el-button>
        </div>
      </el-tab-pane>
    </el-tabs>

    <!-- 向导弹窗（三步） -->
    <el-dialog v-model="wizardVisible" :title="wizardMode === 'create' ? '新建生成配置' : '编辑生成配置'" width="1100px" class="dialog-scroll" top="4vh">
      <el-steps :active="step" align-center finish-status="success" style="margin-bottom: 16px">
        <el-step title="基本信息" />
        <el-step title="字段配置" />
        <el-step title="主子表与生成" />
      </el-steps>

      <!-- Step 1：基本信息 -->
      <div v-show="step === 0">
        <el-form :model="form" :rules="formRules" label-width="110px" style="max-width: 640px">
          <el-form-item label="表名" prop="tableName">
            <el-input v-model="form.tableName" placeholder="biz_xxx" @blur="form.sourceType === 'manual' && columns.length === 0 ? initColumnsFromDb() : undefined" />
          </el-form-item>
          <el-form-item label="表描述" prop="tableComment">
            <el-input v-model="form.tableComment" placeholder="如：合同管理" />
          </el-form-item>
          <el-form-item label="模块名" prop="moduleName">
            <el-input v-model="form.moduleName" placeholder="expense（决定 biz_xxx 前缀/路由/权限码）" />
          </el-form-item>
          <el-form-item label="功能名" prop="functionName">
            <el-input v-model="form.functionName" placeholder="如：报销管理" />
          </el-form-item>
          <el-form-item label="实体名" prop="entityName">
            <el-input v-model="form.entityName" placeholder="Expense（PascalCase）" />
          </el-form-item>
          <el-form-item label="作者">
            <el-input v-model="form.author" style="width: 200px" />
          </el-form-item>
          <el-form-item label="生成选项">
            <el-checkbox v-model="form.dataScope">数据权限列（DeptId/OwnerUserId + IDataScope）</el-checkbox>
            <el-checkbox v-model="form.flowDoc">审批流单据（Status + 提交审批 + 时间线）</el-checkbox>
          </el-form-item>
        </el-form>
        <el-button type="primary" @click="step = 1">下一步：字段配置</el-button>
      </div>

      <!-- Step 2：字段配置 -->
      <div v-show="step === 1">
        <el-alert type="info" :closable="false" style="margin-bottom: 8px" title="主键（id）与审计列自动排除在表单外；勾选“查询”的字段进查询区，“列表”进表格列。" />
        <div class="toolbar">
          <el-button :icon="Plus" @click="addColumn">加字段</el-button>
          <el-button :icon="Refresh" @click="initColumnsFromDb">重新读取库列</el-button>
        </div>
        <el-table :data="columns" border max-height="420">
          <el-table-column label="列名" width="150">
            <template #default="{ row }">
              <el-input v-model="(row as GenColumn).columnName" size="small" placeholder="amount" :disabled="(row as GenColumn).isPk" />
            </template>
          </el-table-column>
          <el-table-column label="属性名" width="140">
            <template #default="{ row }">
              <el-input v-model="(row as GenColumn).propertyName" size="small" placeholder="Amount" :disabled="(row as GenColumn).isPk" />
            </template>
          </el-table-column>
          <el-table-column label="C# 类型" width="120">
            <template #default="{ row }">
              <el-select v-model="(row as GenColumn).cSharpType" size="small" :disabled="(row as GenColumn).isPk">
                <el-option v-for="t in ['string', 'int', 'long', 'decimal', 'DateTime', 'bool']" :key="t" :value="t" :label="t" />
              </el-select>
            </template>
          </el-table-column>
          <el-table-column label="显示名" width="130">
            <template #default="{ row }">
              <el-input v-model="(row as GenColumn).displayName" size="small" placeholder="金额" />
            </template>
          </el-table-column>
          <el-table-column label="控件" width="110">
            <template #default="{ row }">
              <el-select v-model="(row as GenColumn).uiType" size="small" :disabled="(row as GenColumn).isPk">
                <el-option v-for="t in UI_TYPES" :key="t.value" :value="t.value" :label="t.label" />
              </el-select>
            </template>
          </el-table-column>
          <el-table-column label="列表" width="60" align="center">
            <template #default="{ row }">
              <el-checkbox v-model="(row as GenColumn).isList" :disabled="(row as GenColumn).isPk" />
            </template>
          </el-table-column>
          <el-table-column label="查询" width="60" align="center">
            <template #default="{ row }">
              <el-checkbox v-model="(row as GenColumn).isQuery" :disabled="(row as GenColumn).isPk" />
            </template>
          </el-table-column>
          <el-table-column label="必填" width="60" align="center">
            <template #default="{ row }">
              <el-checkbox v-model="(row as GenColumn).isRequired" :disabled="(row as GenColumn).isPk" />
            </template>
          </el-table-column>
          <el-table-column label="操作" width="70" align="center">
            <template #default="{ row, $index }">
              <el-button link type="danger" :disabled="(row as GenColumn).isPk" @click="removeColumn($index)">删</el-button>
            </template>
          </el-table-column>
        <template #empty>
          <TableEmpty />
        </template>
        </el-table>
        <div style="margin-top: 12px; display: flex; gap: 8px">
          <el-button @click="step = 0">上一步</el-button>
          <el-button type="primary" @click="step = 2">下一步：主子表与生成</el-button>
        </div>
      </div>

      <!-- Step 3：主子表与生成 -->
      <div v-show="step === 2">
        <el-alert type="info" :closable="false" style="margin-bottom: 8px"
          title="主子表：添加子表并指定外键列（子表内指向主表 id 的列），生成的页面在主表表单内嵌明细行编辑，保存走事务。" />
        <div class="toolbar">
          <el-button :icon="Plus" @click="addSubTable">加子表</el-button>
        </div>
        <el-collapse>
          <el-collapse-item v-for="(sub, si) in subTables" :key="si" :name="si">
            <template #title>
              <span style="font-weight: 600">{{ sub.tableName || '（未命名子表）' }}</span>
              <el-button link type="danger" style="margin-left: 12px" @click.stop="removeSubTable(si)">移除</el-button>
            </template>
            <el-form inline label-width="80px">
              <el-form-item label="子表名">
                <el-input v-model="sub.tableName" size="small" placeholder="biz_xxx_item" @blur="sub.columns.length === 0 && loadSubColumns(sub)" />
              </el-form-item>
              <el-form-item label="外键列">
                <el-input v-model="sub.fkColumn" size="small" placeholder="expense_id（指向主表 id）" />
              </el-form-item>
              <el-form-item label="实体名">
                <el-input v-model="sub.entityName" size="small" placeholder="ExpenseItem" />
              </el-form-item>
              <el-form-item>
                <el-button size="small" @click="loadSubColumns(sub)">读库列</el-button>
                <el-button size="small" :icon="Plus" @click="addSubColumn(sub)">加字段</el-button>
              </el-form-item>
            </el-form>
            <el-table :data="sub.columns" border size="small" max-height="260">
              <el-table-column label="列名" width="160">
                <template #default="{ row }">
                  <el-input v-model="(row as GenColumn).columnName" size="small" :disabled="(row as GenColumn).isPk" />
                </template>
              </el-table-column>
              <el-table-column label="属性名" width="150">
                <template #default="{ row }">
                  <el-input v-model="(row as GenColumn).propertyName" size="small" :disabled="(row as GenColumn).isPk" />
                </template>
              </el-table-column>
              <el-table-column label="C# 类型" width="120">
                <template #default="{ row }">
                  <el-select v-model="(row as GenColumn).cSharpType" size="small">
                    <el-option v-for="t in ['string', 'int', 'long', 'decimal', 'DateTime', 'bool']" :key="t" :value="t" :label="t" />
                  </el-select>
                </template>
              </el-table-column>
              <el-table-column label="显示名" width="130">
                <template #default="{ row }">
                  <el-input v-model="(row as GenColumn).displayName" size="small" />
                </template>
              </el-table-column>
              <el-table-column label="必填" width="60" align="center">
                <template #default="{ row }">
                  <el-checkbox v-model="(row as GenColumn).isRequired" :disabled="(row as GenColumn).isPk" />
                </template>
              </el-table-column>
              <el-table-column label="操作" width="70" align="center">
                <template #default="{ $index }">
                  <el-button link type="danger" @click="removeSubColumn(sub, $index)">删</el-button>
                </template>
              </el-table-column>
            <template #empty>
              <TableEmpty />
            </template>
            </el-table>
          </el-collapse-item>
        </el-collapse>

        <div style="margin-top: 16px; display: flex; gap: 8px">
          <el-button @click="step = 1">上一步</el-button>
          <el-button @click="handlePreview">预览生成代码</el-button>
          <el-button v-permission="'sys:gentable:add'" type="primary" @click="handleSaveAndClose">保存配置</el-button>
        </div>
      </div>

      <!-- 预览抽屉 -->
      <el-drawer v-model="previewVisible" title="生成代码预览" size="55%">
        <el-tabs v-model="previewActive">
          <el-tab-pane v-for="f in previewFiles" :key="f.path" :label="f.path.split('/').pop()" :name="f.path">
            <div class="preview-path">{{ f.path }}</div>
            <pre class="preview-code">{{ f.content }}</pre>
          </el-tab-pane>
        </el-tabs>
      </el-drawer>
    </el-dialog>
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

.preview-path {
  color: var(--el-text-color-secondary);
  font-size: 12px;
  margin-bottom: 6px;
}

.preview-code {
  background: var(--el-fill-color-light);
  padding: 12px;
  border-radius: 6px;
  font-size: 12px;
  line-height: 1.6;
  overflow: auto;
  max-height: 60vh;
  white-space: pre-wrap;
  word-break: break-all;
}
</style>
