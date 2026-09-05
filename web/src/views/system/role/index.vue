<script setup lang="ts">
import { nextTick, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import type { ElTree } from 'element-plus'
import { Plus, Refresh, Search } from '@element-plus/icons-vue'
import {
  assignRoleMenus,
  createRole,
  deleteRole,
  getRoleMenuIds,
  getRolePage,
  updateRole
} from '@/api/role'
import { getMenuTree } from '@/api/menu'
import { formatDateTime } from '@/utils/format'
import type { MenuTree, Role } from '@/types/api'

const loading = ref(false)
const list = ref<Role[]>([])
const total = ref(0)

const query = reactive({ pageIndex: 1, pageSize: 10, keyword: '', status: undefined as number | undefined })

async function loadData() {
  loading.value = true
  try {
    const page = await getRolePage(query)
    list.value = page.items
    total.value = page.total
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  query.pageIndex = 1
  loadData()
}

function handleReset() {
  query.keyword = ''
  query.status = undefined
  handleSearch()
}

// ---------- 新增 / 编辑 ----------
const dialogVisible = ref(false)
const editingId = ref<number | null>(null)
const formRef = ref<FormInstance>()
const form = reactive({ roleName: '', roleCode: '', status: 1, sort: 0 })

const rules: FormRules = {
  roleName: [{ required: true, message: '请输入角色名称', trigger: 'blur' }],
  roleCode: [{ required: true, message: '请输入角色编码', trigger: 'blur' }]
}

function openCreate() {
  editingId.value = null
  Object.assign(form, { roleName: '', roleCode: '', status: 1, sort: 0 })
  dialogVisible.value = true
}

function openEdit(row: Role) {
  editingId.value = row.id
  Object.assign(form, { roleName: row.roleName, roleCode: row.roleCode, status: row.status, sort: row.sort })
  dialogVisible.value = true
}

async function handleSave() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  if (editingId.value == null) {
    await createRole({ ...form })
    ElMessage.success('创建成功')
  } else {
    await updateRole(editingId.value, { ...form })
    ElMessage.success('更新成功')
  }
  dialogVisible.value = false
  loadData()
}

async function handleDelete(row: Role) {
  await ElMessageBox.confirm(`确定删除角色「${row.roleName}」吗？`, '提示', { type: 'warning' })
  await deleteRole(row.id)
  ElMessage.success('删除成功')
  loadData()
}

// ---------- 分配菜单 ----------
const menuDialogVisible = ref(false)
const menuRoleId = ref<number | null>(null)
const menuRoleName = ref('')
const menuTree = ref<MenuTree[]>([])
const menuTreeRef = ref<InstanceType<typeof ElTree>>()
const menuSaving = ref(false)

const treeProps = { label: 'menuName', children: 'children' }

/** 只提交菜单与按钮节点（父目录由后端树接口自动补全展示，授权以叶子为主） */
function collectCheckedIds(): number[] {
  const tree = menuTreeRef.value
  if (!tree) return []
  const checked = tree.getCheckedKeys()
  const halfChecked = tree.getHalfCheckedKeys()
  return [...checked, ...halfChecked].map(Number)
}

async function openAssignMenus(row: Role) {
  menuRoleId.value = row.id
  menuRoleName.value = row.roleName
  menuDialogVisible.value = true
  await nextTick()

  const [tree, checkedIds] = await Promise.all([getMenuTree(), getRoleMenuIds(row.id)])
  menuTree.value = tree

  // 勾选：叶子节点直接勾选，父节点交给 halfChecked，避免 el-tree 全选联动
  const leafIds = new Set<number>()
  const walk = (nodes: MenuTree[], parentChecked: Set<number>) => {
    nodes.forEach((node) => {
      if (checkedIds.includes(node.id)) parentChecked.add(node.id)
      walk(node.children, parentChecked)
      if (!node.children.length && parentChecked.has(node.id)) leafIds.add(node.id)
    })
  }
  const parentOfChecked = new Set<number>()
  walk(tree, parentOfChecked)
  await nextTick()
  menuTreeRef.value?.setCheckedKeys([...leafIds])
}

async function handleSaveMenus() {
  if (menuRoleId.value == null) return
  menuSaving.value = true
  try {
    await assignRoleMenus(menuRoleId.value, collectCheckedIds())
    ElMessage.success('菜单分配成功')
    menuDialogVisible.value = false
  } finally {
    menuSaving.value = false
  }
}

onMounted(loadData)
</script>

<template>
  <el-card>
    <div class="toolbar">
      <el-input
        v-model="query.keyword"
        placeholder="角色名称/编码"
        clearable
        style="width: 200px"
        :prefix-icon="Search"
        @keyup.enter="handleSearch"
      />
      <el-select v-model="query.status" placeholder="状态" clearable style="width: 120px">
        <el-option label="启用" :value="1" />
        <el-option label="停用" :value="0" />
      </el-select>
      <el-button type="primary" :icon="Search" @click="handleSearch">查询</el-button>
      <el-button :icon="Refresh" @click="handleReset">重置</el-button>
      <el-button v-permission="'sys:role:add'" type="success" :icon="Plus" @click="openCreate">
        新增角色
      </el-button>
    </div>

    <el-table v-loading="loading" :data="list" border stripe>
      <el-table-column prop="roleName" label="角色名称" min-width="140" />
      <el-table-column prop="roleCode" label="角色编码" min-width="140" />
      <el-table-column label="状态" width="90" align="center">
        <template #default="{ row }">
          <el-tag :type="row.status === 1 ? 'success' : 'danger'">
            {{ row.status === 1 ? '启用' : '停用' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="sort" label="排序" width="80" align="center" />
      <el-table-column
        prop="createTime"
        label="创建时间"
        width="165"
        :formatter="formatDateTime"
      />
      <el-table-column label="操作" width="185">
        <template #default="{ row }">
          <el-button v-permission="'sys:role:edit'" link type="primary" @click="openEdit(row)">
            编辑
          </el-button>
          <el-button v-permission="'sys:role:edit'" link type="success" @click="openAssignMenus(row)">
            分配菜单
          </el-button>
          <el-button
            v-permission="'sys:role:delete'"
            link
            type="danger"
            :disabled="row.roleCode === 'admin'"
            @click="handleDelete(row)"
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
      layout="total, sizes, prev, pager, next, jumper"
      :total="total"
      :page-sizes="[10, 20, 50]"
      @current-change="loadData"
      @size-change="handleSearch"
    />

    <!-- 新增/编辑弹窗 -->
    <el-dialog
      v-model="dialogVisible"
      :title="editingId == null ? '新增角色' : `编辑角色：${form.roleName}`"
      width="480px"
    >
      <el-form ref="formRef" :model="form" :rules="rules" label-width="90px">
        <el-form-item label="角色名称" prop="roleName">
          <el-input v-model="form.roleName" />
        </el-form-item>
        <el-form-item label="角色编码" prop="roleCode">
          <el-input v-model="form.roleCode" :disabled="form.roleCode === 'admin'" placeholder="如 manager" />
        </el-form-item>
        <el-form-item label="状态">
          <el-radio-group v-model="form.status">
            <el-radio :value="1">启用</el-radio>
            <el-radio :value="0">停用</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="form.sort" :min="0" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSave">确定</el-button>
      </template>
    </el-dialog>

    <!-- 分配菜单弹窗 -->
    <el-dialog v-model="menuDialogVisible" :title="`分配菜单：${menuRoleName}`" width="480px">
      <el-tree
        ref="menuTreeRef"
        :data="menuTree"
        :props="treeProps"
        node-key="id"
        show-checkbox
        default-expand-all
        :check-strictly="false"
      />
      <template #footer>
        <el-button @click="menuDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="menuSaving" @click="handleSaveMenus">确定</el-button>
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
