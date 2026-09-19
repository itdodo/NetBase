<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import { Plus, Refresh } from '@element-plus/icons-vue'
import { createDept, deleteDept, getDeptTree, updateDept } from '@/api/dept'
import type { DeptSave, DeptTree } from '@/api/dept'
import { formatDateTime } from '@/utils/format'
import { getUserList } from '@/api/user'
import type { User } from '@/types/api'

defineOptions({ name: 'SystemDeptView' })

const loading = ref(false)
const tree = ref<DeptTree[]>([])
const keyword = ref('')

const filteredTree = computed(() => {
  if (!keyword.value) return tree.value
  const match = (nodes: DeptTree[]): DeptTree[] =>
    nodes
      .map((node) => {
        const children = match(node.children)
        if (node.deptName.includes(keyword.value) || children.length > 0) {
          return { ...node, children }
        }
        return null
      })
      .filter((n): n is DeptTree => n !== null)
  return match(tree.value)
})

async function loadData(): Promise<void> {
  loading.value = true
  try {
    tree.value = await getDeptTree()
  } finally {
    loading.value = false
  }
}

// ---------- 编辑 ----------
const dialogVisible = ref(false)
const editingId = ref<number | null>(null)
const userOptions = ref<User[]>([])

/** 选择负责人后自动带出姓名（Leader 展示用，LeaderUserId 供审批流解析） */
function onLeaderChange(userId?: number): void {
  const user = userOptions.value.find((u) => u.id === userId)
  form.leader = user ? user.nickName || user.userName : ''
}

const formRef = ref<FormInstance>()
const form = reactive<DeptSave & { leaderUserId?: number }>({
  parentId: 0,
  deptName: '',
  deptCode: '',
  leader: '',
  leaderUserId: undefined,
  sort: 0,
  status: 1
})

const rules: FormRules = {
  deptName: [{ required: true, message: '请输入部门名称', trigger: 'blur' }],
  deptCode: [{ required: true, message: '请输入部门编码', trigger: 'blur' }]
}

/** 上级部门选项（排除自身与按钮类节点） */
const parentOptions = computed(() => {
  const toNode = (m: DeptTree): DeptTree & { disabled?: boolean } => ({
    ...m,
    disabled: m.id === editingId.value,
    children: m.children.map(toNode)
  })
  return [{ id: 0, deptName: '顶级部门', children: tree.value.map(toNode) } as never]
})

function openCreate(parentId = 0): void {
  editingId.value = null
  Object.assign(form, { parentId, deptName: '', deptCode: '', leader: '', leaderUserId: undefined, sort: 0, status: 1 })
  dialogVisible.value = true
}

function openEdit(row: DeptTree): void {
  editingId.value = row.id
  Object.assign(form, {
    parentId: row.parentId,
    deptName: row.deptName,
    deptCode: row.deptCode,
    leader: row.leader ?? '',
    leaderUserId: (row as unknown as { leaderUserId?: number }).leaderUserId || undefined,
    sort: row.sort,
    status: row.status
  })
  dialogVisible.value = true
}

async function handleSave(): Promise<void> {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return
  if (editingId.value == null) {
    await createDept({ ...form })
    ElMessage.success('创建成功')
  } else {
    await updateDept(editingId.value, { ...form })
    ElMessage.success('更新成功')
  }
  dialogVisible.value = false
  loadData()
}

async function handleDelete(row: DeptTree): Promise<void> {
  await ElMessageBox.confirm(`确定删除部门「${row.deptName}」吗？`, '提示', { type: 'warning' })
  await deleteDept(row.id)
  ElMessage.success('删除成功')
  loadData()
}

onMounted(() => {
  loadData()
  getUserList().then((list) => (userOptions.value = list))
})
</script>

<script lang="ts">
export default {}
</script>

<template>
  <el-card>
    <div class="toolbar">
      <el-input v-model="keyword" placeholder="部门名称过滤" clearable style="width: 200px" />
      <el-button :icon="Refresh" @click="keyword = ''">重置</el-button>
      <el-button v-permission="'sys:dept:add'" type="success" :icon="Plus" @click="openCreate(0)">
        新增部门
      </el-button>
    </div>

    <el-table
      v-loading="loading"
      :data="filteredTree"
      row-key="id"
      border
      default-expand-all
      :tree-props="{ children: 'children' }"
    >
      <el-table-column prop="deptName" label="部门名称" min-width="180" />
      <el-table-column prop="deptCode" label="编码" min-width="110" />
      <el-table-column prop="leader" label="负责人" min-width="100">
        <template #default="{ row }">
          {{ (row as DeptTree).leader || '-' }}
        </template>
      </el-table-column>
      <el-table-column prop="sort" label="排序" width="70" align="center" />
      <el-table-column label="状态" width="80" align="center">
        <template #default="{ row }">
          <el-tag :type="row.status === 1 ? 'success' : 'danger'" size="small">
            {{ row.status === 1 ? '启用' : '停用' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="createTime" label="创建时间" width="165" :formatter="formatDateTime" />
      <el-table-column label="操作" width="210" align="center">
        <template #default="{ row }">
          <el-button
            v-permission="'sys:dept:add'"
            link
            type="success"
            @click="openCreate(row.id)"
          >
            添加下级
          </el-button>
          <el-button v-permission="'sys:dept:edit'" link type="primary" @click="openEdit(row as DeptTree)">
            编辑
          </el-button>
          <el-button v-permission="'sys:dept:delete'" link type="danger" @click="handleDelete(row as DeptTree)">
            删除
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog
      v-model="dialogVisible"
      :title="editingId == null ? '新增部门' : `编辑部门：${form.deptName}`"
      width="520px"
    >
      <el-form ref="formRef" :model="form" :rules="rules" label-width="90px">
        <el-form-item label="上级部门">
          <el-tree-select
            v-model="form.parentId"
            :data="parentOptions"
            :props="{ label: 'deptName', children: 'children', disabled: 'disabled' }"
            node-key="id"
            check-strictly
            default-expand-all
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="部门名称" prop="deptName">
          <el-input v-model="form.deptName" />
        </el-form-item>
        <el-form-item label="部门编码" prop="deptCode">
          <el-input v-model="form.deptCode" :disabled="form.deptCode === 'HQ'" />
        </el-form-item>
        <el-form-item label="负责人">
          <el-select
            v-model="form.leaderUserId"
            filterable
            clearable
            placeholder="选择负责人（选填）"
            style="width: 100%"
            @change="onLeaderChange"
          >
            <el-option v-for="u in userOptions" :key="u.id" :label="u.nickName || u.userName" :value="u.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="form.sort" :min="0" />
        </el-form-item>
        <el-form-item label="状态">
          <el-radio-group v-model="form.status">
            <el-radio :value="1">启用</el-radio>
            <el-radio :value="0">停用</el-radio>
          </el-radio-group>
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
</style>
