<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import { Plus, Refresh, Search } from '@element-plus/icons-vue'
import request from '@/api/request'
import type { PageResult } from '@/types/api'
import { formatDateTime } from '@/utils/format'

defineOptions({ name: 'SystemConfigView' })

interface ConfigInfo {
  id: number
  configKey: string
  configValue: string
  configName: string
  isBuiltIn: boolean
  remark?: string
  createTime: string
}

const API = '/sys/config'
const loading = ref(false)
const list = ref<ConfigInfo[]>([])
const total = ref(0)
const query = reactive({ pageIndex: 1, pageSize: 10, keyword: '' })

async function loadData(): Promise<void> {
  loading.value = true
  try {
    const page = await request.get<never, PageResult<ConfigInfo>>(`${API}/page`, { params: query })
    list.value = page.items
    total.value = page.total
  } finally {
    loading.value = false
  }
}

function handleSearch(): void {
  query.pageIndex = 1
  loadData()
}

function handleReset(): void {
  query.keyword = ''
  handleSearch()
}

// ---------- 编辑 ----------
const dialogVisible = ref(false)
const editingId = ref<number | null>(null)
const formRef = ref<FormInstance>()
const form = reactive({ configKey: '', configValue: '', configName: '', remark: '' })

const rules: FormRules = {
  configKey: [{ required: true, message: '请输入参数键', trigger: 'blur' }],
  configValue: [{ required: true, message: '请输入参数值', trigger: 'blur' }],
  configName: [{ required: true, message: '请输入参数名称', trigger: 'blur' }]
}

function openCreate(): void {
  editingId.value = null
  Object.assign(form, { configKey: '', configValue: '', configName: '', remark: '' })
  dialogVisible.value = true
}

function openEdit(row: ConfigInfo): void {
  editingId.value = row.id
  Object.assign(form, {
    configKey: row.configKey,
    configValue: row.configValue,
    configName: row.configName,
    remark: row.remark ?? ''
  })
  dialogVisible.value = true
}

async function handleSave(): Promise<void> {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return
  if (editingId.value == null) {
    await request.post(API, { ...form })
    ElMessage.success('创建成功')
  } else {
    await request.put(`${API}/${editingId.value}`, { ...form })
    ElMessage.success('更新成功')
  }
  dialogVisible.value = false
  loadData()
}

async function handleDelete(row: ConfigInfo): Promise<void> {
  await ElMessageBox.confirm(`确定删除参数「${row.configName}」吗？`, '提示', { type: 'warning' })
  await request.delete(`${API}/${row.id}`)
  ElMessage.success('删除成功')
  loadData()
}

onMounted(loadData)
</script>

<template>
  <el-card>
    <div class="toolbar">
      <el-input
        v-model="query.keyword"
        placeholder="参数键/名称"
        clearable
        style="width: 200px"
        :prefix-icon="Search"
        @keyup.enter="handleSearch"
      />
      <el-button type="primary" :icon="Search" @click="handleSearch">查询</el-button>
      <el-button :icon="Refresh" @click="handleReset">重置</el-button>
      <el-button v-permission="'sys:config:add'" type="success" :icon="Plus" @click="openCreate">
        新增参数
      </el-button>
    </div>

    <el-table v-loading="loading" :data="list" border stripe>
      <el-table-column prop="configName" label="参数名称" min-width="140" />
      <el-table-column prop="configKey" label="参数键" min-width="180" show-overflow-tooltip />
      <el-table-column prop="configValue" label="参数值" min-width="140" show-overflow-tooltip />
      <el-table-column label="内置" width="70" align="center">
        <template #default="{ row }">
          <el-tag :type="row.isBuiltIn ? 'primary' : 'info'" size="small">
            {{ row.isBuiltIn ? '是' : '否' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="remark" label="备注" min-width="160" show-overflow-tooltip />
      <el-table-column prop="createTime" label="创建时间" width="165" :formatter="formatDateTime" />
      <el-table-column label="操作" width="140" align="center">
        <template #default="{ row }">
          <el-button v-permission="'sys:config:edit'" link type="primary" @click="openEdit(row as ConfigInfo)">编辑</el-button>
          <el-button
            v-permission="'sys:config:delete'"
            link
            type="danger"
            :disabled="row.isBuiltIn"
            @click="handleDelete(row as ConfigInfo)"
          >
            删除
          </el-button>
        </template>
      </el-table-column>
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

    <el-dialog
      v-model="dialogVisible"
      :title="editingId == null ? '新增参数' : `编辑参数：${form.configName}`"
      width="500px"
    >
      <el-form ref="formRef" :model="form" :rules="rules" label-width="90px">
        <el-form-item label="参数键" prop="configKey">
          <el-input v-model="form.configKey" placeholder="如 sys.pwd.defaultPassword" />
        </el-form-item>
        <el-form-item label="参数值" prop="configValue">
          <el-input v-model="form.configValue" />
        </el-form-item>
        <el-form-item label="参数名称" prop="configName">
          <el-input v-model="form.configName" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="form.remark" type="textarea" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSave">确定</el-button>
      </template>
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
</style>
