<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Refresh, Search } from '@element-plus/icons-vue'
import type { Position } from '@/api/position'
import { createPosition, deletePosition, updatePosition } from '@/api/position'
import { formatDateTime } from '@/utils/format'
import { usePageList } from '@/composables/usePageList'

defineOptions({ name: 'SystemPositionView' })

const {
  loading,
  list,
  total,
  query,
  loadData,
  handleSearch
} = usePageList<Position, { pageIndex: number; pageSize: number; keyword: string }>({
  url: '/sys/position/page',
  defaultQuery: { pageIndex: 1, pageSize: 10, keyword: '' }
})

function handleReset(): void {
  query.keyword = ''
  handleSearch()
}

// ---------- 新增 / 编辑 ----------
const dialogVisible = ref(false)
const saving = ref(false)
const editingId = ref<string | null>(null)
const formRef = ref()
const form = ref({ positionCode: '', positionName: '', sort: 0, status: 1, remark: '' })
const rules = {
  positionCode: [{ required: true, message: '请输入岗位编码', trigger: 'blur' }],
  positionName: [{ required: true, message: '请输入岗位名称', trigger: 'blur' }]
}

function openCreate(): void {
  editingId.value = null
  form.value = { positionCode: '', positionName: '', sort: 0, status: 1, remark: '' }
  dialogVisible.value = true
}

function openEdit(row: Position): void {
  editingId.value = row.id
  form.value = {
    positionCode: row.positionCode,
    positionName: row.positionName,
    sort: row.sort,
    status: row.status,
    remark: row.remark ?? ''
  }
  dialogVisible.value = true
}

async function handleSave(): Promise<void> {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return
  saving.value = true
  try {
    if (editingId.value == null) {
      await createPosition({ ...form.value })
      ElMessage.success('创建成功')
    } else {
      await updatePosition(editingId.value, { ...form.value })
      ElMessage.success('更新成功')
    }
    dialogVisible.value = false
    loadData()
  } finally {
    saving.value = false
  }
}

async function handleDelete(row: Position): Promise<void> {
  await ElMessageBox.confirm(`确定删除岗位「${row.positionName}」吗？`, '提示', { type: 'warning' })
  await deletePosition(row.id)
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
        placeholder="岗位名称/编码"
        clearable
        style="width: 200px"
        :prefix-icon="Search"
        @keyup.enter="handleSearch"
      />
      <el-button type="primary" :icon="Search" @click="handleSearch">查询</el-button>
      <el-button :icon="Refresh" @click="handleReset">重置</el-button>
      <el-button v-permission="'sys:position:add'" type="primary" :icon="Plus" @click="openCreate">新建岗位</el-button>
    </div>

    <el-table v-loading="loading" :data="list" border stripe>
      <el-table-column prop="positionCode" label="岗位编码" min-width="130" />
      <el-table-column prop="positionName" label="岗位名称" min-width="150" />
      <el-table-column label="状态" width="80" align="center">
        <template #default="{ row }">
          <el-tag :type="(row as Position).status === 1 ? 'success' : 'info'" size="small">
            {{ (row as Position).status === 1 ? '启用' : '停用' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="remark" label="备注" min-width="140" show-overflow-tooltip />
      <el-table-column prop="createTime" label="创建时间" width="165" :formatter="formatDateTime" />
      <el-table-column label="操作" width="140" align="center">
        <template #default="{ row }">
          <el-button v-permission="'sys:position:edit'" link type="primary" @click="openEdit(row as Position)">编辑</el-button>
          <el-button v-permission="'sys:position:delete'" link type="danger" @click="handleDelete(row as Position)">删除</el-button>
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

    <el-dialog v-model="dialogVisible" :title="editingId == null ? '新建岗位' : '编辑岗位'" width="480px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="80px">
        <el-form-item label="岗位编码" prop="positionCode">
          <el-input v-model="form.positionCode" :disabled="editingId != null" placeholder="如 manager" maxlength="50" />
        </el-form-item>
        <el-form-item label="岗位名称" prop="positionName">
          <el-input v-model="form.positionName" maxlength="50" />
        </el-form-item>
        <el-form-item label="排序号">
          <el-input-number v-model="form.sort" :min="0" style="width: 100%" />
        </el-form-item>
        <el-form-item label="状态">
          <el-radio-group v-model="form.status">
            <el-radio :value="1">启用</el-radio>
            <el-radio :value="0">停用</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="form.remark" type="textarea" :rows="2" maxlength="200" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">保存</el-button>
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
