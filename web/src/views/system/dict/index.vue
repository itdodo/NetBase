<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import { Plus } from '@element-plus/icons-vue'
import request from '@/api/request'
import type { PageQuery, PageResult } from '@/types/api'
interface DictDataDto {
  id: number
  label: string
  value: string
  sort: number
  status: number
  dictCode?: string
}

defineOptions({ name: 'SystemDictView' })

interface DictType {
  id: number
  dictCode: string
  dictName: string
  status: number
  remark?: string
  createTime: string
}

const API = '/sys/dict'

// ---------- 类型 ----------
const typeLoading = ref(false)
const types = ref<DictType[]>([])
const typeTotal = ref(0)
const typeQuery = reactive({ pageIndex: 1, pageSize: 10, keyword: '' })
const selectedType = ref<DictType | null>(null)

async function loadTypes(): Promise<void> {
  typeLoading.value = true
  try {
    const page = await request.get<never, PageResult<DictType>>(`${API}/type/page`, { params: typeQuery })
    types.value = page.items
    typeTotal.value = page.total
    if (!selectedType.value && page.items.length > 0) {
      selectType(page.items[0])
    }
  } finally {
    typeLoading.value = false
  }
}

function selectType(type: unknown): void {
  if (!type) return
  selectedType.value = type as DictType
  dataQuery.pageIndex = 1
  loadData()
}

// ---------- 类型编辑 ----------
const typeDialogVisible = ref(false)
const typeEditingId = ref<number | null>(null)
const typeFormRef = ref<FormInstance>()
const typeForm = reactive({ dictCode: '', dictName: '', status: 1, remark: '' })

const typeRules: FormRules = {
  dictCode: [{ required: true, message: '请输入字典编码', trigger: 'blur' }],
  dictName: [{ required: true, message: '请输入字典名称', trigger: 'blur' }]
}

function openTypeCreate(): void {
  typeEditingId.value = null
  Object.assign(typeForm, { dictCode: '', dictName: '', status: 1, remark: '' })
  typeDialogVisible.value = true
}

function openTypeEdit(row: DictType): void {
  typeEditingId.value = row.id
  Object.assign(typeForm, { dictCode: row.dictCode, dictName: row.dictName, status: row.status, remark: row.remark ?? '' })
  typeDialogVisible.value = true
}

async function saveType(): Promise<void> {
  const valid = await typeFormRef.value?.validate().catch(() => false)
  if (!valid) return
  if (typeEditingId.value == null) {
    await request.post(`${API}/type`, { ...typeForm })
    ElMessage.success('创建成功')
  } else {
    await request.put(`${API}/type/${typeEditingId.value}`, { ...typeForm })
    ElMessage.success('更新成功')
  }
  typeDialogVisible.value = false
  loadTypes()
}

async function deleteType(row: DictType): Promise<void> {
  await ElMessageBox.confirm(`删除字典「${row.dictName}」及其全部数据项？`, '提示', { type: 'warning' })
  await request.delete(`${API}/type/${row.id}`)
  ElMessage.success('删除成功')
  selectedType.value = null
  loadTypes()
}

// ---------- 数据项 ----------
const dataLoading = ref(false)
const dataList = ref<DictDataDto[]>([])
const dataTotal = ref(0)
const dataQuery = reactive<PageQuery>({ pageIndex: 1, pageSize: 10 })

async function loadData(): Promise<void> {
  if (!selectedType.value) return
  dataLoading.value = true
  try {
    const page = await request.get<never, PageResult<DictDataDto>>(`${API}/data/page`, {
      params: { dictTypeId: selectedType.value.id, ...dataQuery }
    })
    dataList.value = page.items
    dataTotal.value = page.total
  } finally {
    dataLoading.value = false
  }
}

// ---------- 数据项编辑 ----------
const dataDialogVisible = ref(false)
const dataEditingId = ref<number | null>(null)
const dataFormRef = ref<FormInstance>()
const dataForm = reactive({ label: '', value: '', sort: 0, status: 1, remark: '' })

const dataRules: FormRules = {
  label: [{ required: true, message: '请输入标签', trigger: 'blur' }],
  value: [{ required: true, message: '请输入存储值', trigger: 'blur' }]
}

function openDataCreate(): void {
  if (!selectedType.value) {
    ElMessage.warning('请先选择左侧字典类型')
    return
  }
  dataEditingId.value = null
  Object.assign(dataForm, { label: '', value: '', sort: dataList.value.length + 1, status: 1, remark: '' })
  dataDialogVisible.value = true
}

function openDataEdit(row: DictDataDto): void {
  dataEditingId.value = row.id
  Object.assign(dataForm, { label: row.label, value: row.value, sort: row.sort, status: row.status, remark: '' })
  dataDialogVisible.value = true
}

async function saveData(): Promise<void> {
  const valid = await dataFormRef.value?.validate().catch(() => false)
  if (!valid || !selectedType.value) return
  const payload = { ...dataForm, dictTypeId: selectedType.value.id }
  if (dataEditingId.value == null) {
    await request.post(`${API}/data`, payload)
    ElMessage.success('创建成功')
  } else {
    await request.put(`${API}/data/${dataEditingId.value}`, payload)
    ElMessage.success('更新成功')
  }
  dataDialogVisible.value = false
  loadData()
}

async function deleteData(row: DictDataDto): Promise<void> {
  await ElMessageBox.confirm(`删除字典项「${row.label}」？`, '提示', { type: 'warning' })
  await request.delete(`${API}/data/${row.id}`)
  ElMessage.success('删除成功')
  loadData()
}

onMounted(loadTypes)
  import TableEmpty from '@/components/TableEmpty.vue'
</script>

<template>
  <el-card>
    <el-row :gutter="16">
      <!-- 左：字典类型 -->
      <el-col :span="9">
        <div class="panel-title">
          字典类型
          <el-button v-permission="'sys:dict:add'" type="primary" size="small" :icon="Plus" @click="openTypeCreate">
            新增
          </el-button>
        </div>
        <el-table
          v-loading="typeLoading"
          :data="types"
          border
          highlight-current-row
          @current-change="selectType"
        >
          <el-table-column prop="dictName" label="名称" min-width="100" />
          <el-table-column prop="dictCode" label="编码" min-width="110" />
          <el-table-column label="状态" width="70" align="center">
            <template #default="{ row }">
              <el-tag :type="row.status === 1 ? 'success' : 'danger'" size="small">
                {{ row.status === 1 ? '启用' : '停用' }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="操作" width="120" align="center">
            <template #default="{ row }">
              <el-button v-permission="'sys:dict:edit'" link type="primary" @click="openTypeEdit(row as DictType)">编辑</el-button>
              <el-button v-permission="'sys:dict:delete'" link type="danger" @click="deleteType(row as DictType)">删除</el-button>
            </template>
          </el-table-column>
        <template #empty>
          <TableEmpty />
        </template>
        </el-table>
        <el-pagination
          v-model:current-page="typeQuery.pageIndex"
          v-model:page-size="typeQuery.pageSize"
          class="pagination"
          background
          layout="total, prev, pager, next"
          :total="typeTotal"
          @current-change="loadTypes"
        />
      </el-col>

      <!-- 右：数据项 -->
      <el-col :span="15">
        <div class="panel-title">
          数据项{{ selectedType ? `（${selectedType.dictName}）` : '' }}
          <el-button v-permission="'sys:dict:add'" type="primary" size="small" :icon="Plus" @click="openDataCreate">
            新增
          </el-button>
        </div>
        <el-table v-loading="dataLoading" :data="dataList" border>
          <el-table-column prop="label" label="标签" min-width="110" />
          <el-table-column prop="value" label="存储值" min-width="110" />
          <el-table-column prop="sort" label="排序" width="70" align="center" />
          <el-table-column label="状态" width="70" align="center">
            <template #default="{ row }">
              <el-tag :type="row.status === 1 ? 'success' : 'danger'" size="small">
                {{ row.status === 1 ? '启用' : '停用' }}
              </el-tag>
            </template>
          </el-table-column>
          <el-table-column label="操作" width="120" align="center">
            <template #default="{ row }">
              <el-button v-permission="'sys:dict:edit'" link type="primary" @click="openDataEdit(row as DictDataDto)">编辑</el-button>
              <el-button v-permission="'sys:dict:delete'" link type="danger" @click="deleteData(row as DictDataDto)">删除</el-button>
            </template>
          </el-table-column>
        <template #empty>
          <TableEmpty />
        </template>
        </el-table>
        <el-pagination
          v-model:current-page="dataQuery.pageIndex"
          v-model:page-size="dataQuery.pageSize"
          class="pagination"
          background
          layout="total, prev, pager, next"
          :total="dataTotal"
          @current-change="loadData"
        />
      </el-col>
    </el-row>

    <!-- 类型编辑 -->
    <el-dialog v-model="typeDialogVisible" :title="typeEditingId == null ? '新增字典类型' : '编辑字典类型'" width="460px">
      <el-form ref="typeFormRef" :model="typeForm" :rules="typeRules" label-width="90px">
        <el-form-item label="字典编码" prop="dictCode">
          <el-input v-model="typeForm.dictCode" placeholder="如 demo_priority" />
        </el-form-item>
        <el-form-item label="字典名称" prop="dictName">
          <el-input v-model="typeForm.dictName" />
        </el-form-item>
        <el-form-item label="状态">
          <el-radio-group v-model="typeForm.status">
            <el-radio :value="1">启用</el-radio>
            <el-radio :value="0">停用</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="typeForm.remark" type="textarea" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="typeDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="saveType">确定</el-button>
      </template>
    </el-dialog>

    <!-- 数据项编辑 -->
    <el-dialog v-model="dataDialogVisible" :title="dataEditingId == null ? '新增数据项' : '编辑数据项'" width="460px">
      <el-form ref="dataFormRef" :model="dataForm" :rules="dataRules" label-width="90px">
        <el-form-item label="标签" prop="label">
          <el-input v-model="dataForm.label" />
        </el-form-item>
        <el-form-item label="存储值" prop="value">
          <el-input v-model="dataForm.value" />
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="dataForm.sort" :min="0" />
        </el-form-item>
        <el-form-item label="状态">
          <el-radio-group v-model="dataForm.status">
            <el-radio :value="1">启用</el-radio>
            <el-radio :value="0">停用</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="dataForm.remark" type="textarea" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dataDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="saveData">确定</el-button>
      </template>
    </el-dialog>
  </el-card>
</template>

<style scoped>
.panel-title {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 10px;
  font-weight: 600;
}

.pagination {
  margin-top: 10px;
  justify-content: flex-end;
}
</style>
